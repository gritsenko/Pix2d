using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using Mvvm;
using Mvvm.Messaging;
using Pix2d.CommonNodes;
using Pix2d.Messages;
using Pix2d.Messages.Edit;
using Pix2d.Modules.Sprite.Editors;
using Pix2d.Mvvm;
using Pix2d.Plugins.Sprite.Editors;
using Pix2d.Primitives.SpriteEditor;
using Pix2d.ViewModels.Animations;
using SkiaSharp;

namespace Pix2d.Plugins.Sprite.ViewModels.Animation;

public class SpriteAnimationTimelineViewModel : Pix2dViewModelBase
{
    private SpriteEditor _editor;
    private AnimationFrameViewModel _currentFrame;
    private bool _reorderingStarted;
    private ItemReorderInfo<AnimationFrameViewModel> _reorderInfo;
    public IMessenger Messenger { get; }
    public AppState AppState { get; }

    public ObservableCollection<AnimationFrameViewModel> Frames { get; set; } = new();

    public AnimationFrameViewModel CurrentFrame
    {
        get => _currentFrame;
        set
        {
            if (_currentFrame != value)
            {
                if (_currentFrame != null)
                {
                    _currentFrame.IsSelected = false;
                }

                _currentFrame = value;

                if (_currentFrame != null)
                {
                    _currentFrame.IsSelected = true;
                }

                if (_editor != null && _currentFrame != null && Frames.Contains(_currentFrame))
                {
                    _editor.SetFrameIndex(Frames.IndexOf(_currentFrame));
                }
                OnPropertyChanged(nameof(CurrentFrame));

                if (!IsPlaying)
                {
                    RunInUiThread(() =>
                    {
                        DeleteFrameCommand.RaiseCanExecuteChanged();
                    });
                }
            }
        }
    }

    public int FrameCount => Frames.Count;

    [NotifiesOn(nameof(CurrentFrame))]
    public int FrameIndex => Frames.IndexOf(CurrentFrame) + 1;

    [NotifiesOn(nameof(FrameIndex))]
    [NotifiesOn(nameof(FrameCount))]
    public string FrameInfo => $"{FrameIndex} / {FrameCount}";
        
    public List<int> FrameRates { get; set; } = new List<int>();

    public int SelectedFramerate
    {
        get => _editor?.FrameRate ?? 0;
        set
        {
            if (_editor.FrameRate != value)
            {
                _editor.FrameRate = value;
                SessionLogger.OpLog(value.ToString());
                OnPropertyChanged();
            }
        }
    }

    public bool ShowOnionSkin
    {
        get => _editor?.ShowOnionSkin ?? false;
        set
        {
            if (_editor.ShowOnionSkin != value)
            {
                _editor.ShowOnionSkin = value;
                SessionLogger.OpLog(value.ToString());
                OnPropertyChanged();
            }
        }
    }

    public bool IsPlaying => _editor?.IsPlaying ?? false;

    [NotifiesOn(nameof(CurrentFrame))]
    public IRelayCommand DeleteFrameCommand => GetCommand(() =>
    {
        _editor.DeleteFrame();
    }, () => (_editor?.FramesCount ?? 0) > 1);

    public SpriteAnimationTimelineViewModel(IMessenger messenger, AppState appState)
    {
        Messenger = messenger;
        AppState = appState;

        messenger.Register<OperationInvokedMessage>(this, OnOperationInvoked);
        messenger.Register<NodeEditorChangedMessage>(this, OnEditorChanged);
        Messenger.Register<CanvasSizeChanged>(this, msg => OnLoad());

        UpdateEditor();

        InitFrameRates();

        Frames.CollectionChanged += Frames_CollectionChanged;
    }

    private void OnOperationInvoked(OperationInvokedMessage obj)
    {
        if (CurrentFrame != null)
        {
            CurrentFrame.UpdatePreview();
            CurrentFrame.UpdateProperties();
        }
    }

    private void OnEditorChanged(NodeEditorChangedMessage obj)
    {
        UpdateEditor();
    }

    private void Frames_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Remove)
        {
            _reorderInfo = new ItemReorderInfo<AnimationFrameViewModel>()
            {
                Items = e.OldItems.OfType<AnimationFrameViewModel>().ToArray(),
                OldIndex = e.OldStartingIndex
            };
            _reorderingStarted = true;
        }

        if (e.Action == NotifyCollectionChangedAction.Add && _reorderingStarted)
        {
            _reorderInfo.NewIndex = e.NewStartingIndex;
            OnFramesReordered(_reorderInfo);
            _reorderingStarted = false;
            _reorderInfo = null;
        }
    }

    private void OnFramesReordered(ItemReorderInfo<AnimationFrameViewModel> reorderInfo)
    {
        Debug.WriteLine($"Reordered frmaes from {reorderInfo.OldIndex} to {reorderInfo.NewIndex}");
        _editor.ReorderFrames(reorderInfo.OldIndex, reorderInfo.NewIndex);
    }

    private void InitFrameRates()
    {
        for (int i = 0; i <= 60; i++)
        {
            FrameRates.Add(i);
        }
    }

    private void UpdateEditor()
    {
        if (AppState.CurrentProject.CurrentContextType != EditContextType.Sprite)
            return;

        var newEditor = AppState.CurrentProject.CurrentNodeEditor as SpriteEditor;
        if (_editor != newEditor)
        {
            if (_editor != null)
            {
                _editor.FramesChanged -= EditorOnFramesChanged;
                _editor.PlaybackStateChanged -= EditorOnPlaybackStateChanged;
                _editor.CurrentFrameChanged -= EditorOnCurrentFrameChanged;
                _editor.LayersChanged -= EditorOnLayersChanged;
            }

            _editor = newEditor;

            if (_editor != null)
            {
                _editor.FramesChanged += EditorOnFramesChanged;
                _editor.PlaybackStateChanged += EditorOnPlaybackStateChanged;
                _editor.CurrentFrameChanged += EditorOnCurrentFrameChanged;
                _editor.LayersChanged += EditorOnLayersChanged;
            }
            OnLoad();
        }
    }

    private void EditorOnLayersChanged(object sender, EventArgs e)
    {
        foreach (var frame in Frames)
        {
            frame.UpdatePreview();
            frame.UpdateProperties();
        }
    }

    private void EditorOnCurrentFrameChanged(object sender, SpriteFrameChangedEvenArgs e)
    {
        if (Frames.Count > _editor.CurrentFrameIndex)
            CurrentFrame = Frames[_editor.CurrentFrameIndex];
    }

    private void EditorOnPlaybackStateChanged(object sender, EventArgs e)
    {
        CurrentFrame = Frames[_editor.CurrentFrameIndex];

        OnPropertyChanged(nameof(IsPlaying));
        OnPropertyChanged(nameof(CurrentFrame));
    }

    private void EditorOnFramesChanged(object sender, FramesChangedEventArgs e)
    {
        if (e.ChangeType == FramesChangedType.Reset)
        {
            OnLoad(); 
        }
        else if (e.ChangeType == FramesChangedType.Add)
        {
            foreach (var index in e.AffectedIndexes)
            {
                AddFrameVm(index);
            }
            CurrentFrame = Frames[_editor.CurrentFrameIndex];
            OnPropertyChanged(nameof(FrameCount));
        }
        else if (e.ChangeType == FramesChangedType.Delete)
        {
            OnLoad();
        }
    }


    protected  override void OnLoad()
    {
        Frames.Clear();

        if (_editor == null)
            return;

        var max = _editor.FramesCount;

        if (max > 0)
        {

            for (var i = 0; i < max; i++)
            {
                AddFrameVm(i);
            }

            CurrentFrame = Frames[_editor.CurrentFrameIndex];

        }
        OnPropertyChanged(nameof(SelectedFramerate));
        OnPropertyChanged(nameof(ShowOnionSkin));
        OnPropertyChanged(nameof(FrameCount));
        DeleteFrameCommand.RaiseCanExecuteChanged();
    }

    private AnimationFrameViewModel AddFrameVm(int index)
    {
        var frameVm = new AnimationFrameViewModel();
        frameVm.PreviewProvider = PreviewProvider;
        frameVm.UpdatePropertiesAction = UpdateFrameProperties;
        if (index > Frames.Count)
            Frames.Add(frameVm);
        else
        {
            Frames.Insert(index, frameVm);
        }

        frameVm.UpdateProperties();

        return frameVm;
    }

    private void UpdateFrameProperties(AnimationFrameViewModel frameVm)
    {
        var index = Frames.IndexOf(frameVm);
        if (index < 0)
        {
            return;
        }

        frameVm.Layers = new List<LayerFrameMeta>();
        foreach (var currentSpriteLayer in _editor.CurrentSprite.Layers.Reverse())
        {
            if (index < currentSpriteLayer.Frames.Count)
            {
                frameVm.Layers.Add(new LayerFrameMeta()
                {
                    IsKeyFrame = currentSpriteLayer.IsKeyFrame(index)
                });
            }
        }
    }

    private SKBitmap PreviewProvider(AnimationFrameViewModel frameVm)
    {
        var index = Frames.IndexOf(frameVm);
        if (index < 0)
        {
            return null;
        }
        var sprite = _editor.CurrentSprite;
        //Debug.WriteLine("preview frame " + index);
        var pw = 48;
        var bitmap = new SKBitmap(new SKImageInfo(pw, pw, SKColorType.Bgra8888));
        var scale = 1f;
        var w = sprite.Size.Width;
        var h = sprite.Size.Height;
        // if (w > pw || h > pw)
        // {
        scale = w > h
            ? pw / w
            : pw / h;
        // }
        sprite.RenderFramePreview(index, ref bitmap, scale, false);
        return bitmap;
        //return _editor.GetFramePreview(index, new SKSize(48, 48));
    }
}