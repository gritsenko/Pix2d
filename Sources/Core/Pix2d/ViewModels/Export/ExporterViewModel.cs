using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Mvvm;
using SkiaNodes;

namespace Pix2d.ViewModels.Export;

public abstract class ExporterViewModel : ViewModelBase
{
    public IDialogService DialogService { get; }
    public string Name { get; set; }
    public virtual bool ShowFileName { get; set; }
    public virtual bool ShowSpritesheetOptions { get; set; }

    public string FileName { get; set; }
    public ExporterViewModel(IDialogService dialogService)
    {
        DialogService = dialogService;
    }

    public virtual async Task Export(IEnumerable<SKNode> nodes, ImageExportSettings settings)
    {
        try
        {
            Commands.View.HideExportDialogCommand.Execute();
        }
        catch (Exception e)
        {
            Logger.LogException(e);
        }
    }

    public abstract Task<Stream> ExportToStream(IEnumerable<SKNode> node, ImageExportSettings settings);

    public virtual void OnSelected()
    {
            
    }

    public virtual void Reset()
    {
            
    }

}