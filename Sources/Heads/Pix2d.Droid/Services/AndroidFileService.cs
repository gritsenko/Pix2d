using Android.Content;
using Android.Views.InputMethods;
using Pix2d.Abstract;
using Pix2d.Abstract.Platform.FileSystem;
using Pix2d.Primitives;
using SkiaNodes.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Pix2d.Android.Services;

namespace Pix2d.Services
{
    public class AndroidFileService : SharedFileService
    {
        Dictionary<string, string> _contextPaths = new Dictionary<string, string>();

        private bool _isDialogOpened;
        private readonly Dictionary<string, IDataStorage> _storages = new Dictionary<string, IDataStorage>();

        public AndroidFileService()
        {
            var cts = DefaultServiceLocator.ServiceLocatorProvider().GetInstance<ISettingsService>().Get<Dictionary<string, string>>("fileServiceContexts");
            if (cts != null)
            {
                _contextPaths = cts;
            }

            //MainActivity.Instance.ActivityResult += InstanceOnActivityResult;
        }

        //void InstanceOnActivityResult(object sender, BaseActivity.ActivityResultEventArgs e)
        //{

        //    var resolver = MainActivity.Instance.ContentResolver;
        //    //OPEN FILE
        //    if (e.ResultCode == Result.Ok && e.RequestCode == 44)
        //    {
        //        var uri = e.Data.Data;


        //        var displayName = e.Data.DataString;
        //        if (AndroidFileContentSource.TryGetFileNameFromUri(uri, out var fileName))
        //        {
        //            TakePermissions(resolver, uri);

        //            displayName = fileName;
        //            var type = Path.GetExtension(fileName);

        //            var fcs = new AndroidFileContentSource(type, uri);
        //            fcs.Title = displayName;

        //            _openFileTcs.SetResult(new[] { fcs });
        //            return;
        //        }

        //        _openFileTcs.SetResult(null);
        //        return;

        //    }

        //    //SAVE FILE
        //    if (e.ResultCode == Result.Ok && e.RequestCode == 43)
        //    {
        //        var uri = e.Data.Data;
        //        var fcs = new AndroidFileContentSource(_tcs.FileType, uri);

        //        var displayName = e.Data.DataString;
        //        if (AndroidFileContentSource.TryGetFileNameFromUri(uri, out var fileName)) displayName = fileName;
        //        fcs.Title = displayName;

        //        TakePermissions(resolver, uri);

        //        _tcs.SetResult(fcs);
        //    }
        //    else
        //    {
        //        _tcs?.SetResult(null);
        //    }

        //    HideKeyboard();
        //}

        private TaskCompletionSource<IEnumerable<IFileContentSource>> _openFileTcs;
        public override Task<IEnumerable<IFileContentSource>> OpenFileWithDialogAsync(string[] fileTypeFilter, bool allowMultiplyFiles = false,
            string contextKey = null)
        {
            //if (fileTypeFilter.Contains(".pix2d"))
            //{
            //    return await base.OpenFileWithDialogAsync(fileTypeFilter, allowMultiplyFiles, contextKey);
            //}

            // проверяем и предотвращаем повтороное открытие диалога
            if (_isDialogOpened)
                return null;

            try
            {
                OnFileDialogOpened();

                var fileType = fileTypeFilter[0];

                var intent = new Intent(Intent.ActionOpenDocument);
                intent.AddCategory(Intent.CategoryOpenable);
                intent.SetFlags(ActivityFlags.GrantReadUriPermission
                                | ActivityFlags.GrantWriteUriPermission
                                | ActivityFlags.GrantPersistableUriPermission
                                | ActivityFlags.GrantPrefixUriPermission);

                //intent.SetType(GetMimeType(fileType));
                intent.SetType("*/*");

                //intent.PutExtra(Intent.ExtraTitle, defaultFileName);
                MainActivity.Instance.StartActivityForResult(Intent.CreateChooser(intent, "Select file to open"), 44);
                _openFileTcs = new AccessFilesTcs();
                return _openFileTcs.Task;



                //var fileData = await CrossFilePicker.Current.PickFile();
                //if (fileData == null)
                //    return null; // user canceled file picking

                //string fileName = fileData.FileName;
                //string contents = System.Text.Encoding.UTF8.GetString(fileData.DataArray);

                //Console.WriteLine("File name chosen: " + fileName);
                //Console.WriteLine("File data: " + contents);

                //var resolver = MainActivity.Instance.ContentResolver;
                //var perms = resolver.PersistedUriPermissions;

                //resolver.TakePersistableUriPermission(Android.Net.Uri.Parse(fileData.FilePath), ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);

                //return new[] { new CrossPickerFileSource(fileData) };
            }
            finally
            {
                OnFileDialogClosed();
            }
        }

        private class AccessFileTcs : TaskCompletionSource<IFileContentSource>
        {
            public string FileType;
            public bool TakeFutureAccessPermissions = false;
            public AccessFileTcs(string fileType, bool takeFutureAccessPermissions)
            {
                TakeFutureAccessPermissions = takeFutureAccessPermissions;
                FileType = fileType;
            }
        }
        private class AccessFilesTcs : TaskCompletionSource<IEnumerable<IFileContentSource>>
        {
        }

        public async void HideKeyboard()
        {
            await Task.Delay(400);
            var imm = (InputMethodManager)MainActivity.Instance.GetSystemService(Context.InputMethodService);
            var token = MainActivity.Instance.CurrentFocus?.WindowToken;
            imm.HideSoftInputFromWindow(token, HideSoftInputFlags.None);
        }


        private AccessFileTcs _tcs;
        public override Task<IFileContentSource> GetFileToSaveWithDialogAsync(string defaultFileName, string[] fileTypeFilter, string contextKey = null)
        {
            //Intent intentShareFile = new Intent(Intent.ActionSend);
            //intentShareFile.SetType("application/zip");

            //var url = "";
            //intentShareFile.PutExtra(Intent.ExtraStream, url);

            //intentShareFile.PutExtra(Intent.ExtraSubject, "Sharing File...");
            //intentShareFile.PutExtra(Intent.ExtraText, "Sharing File...");

            //MainActivity.Instance.StartActivityForResult(Intent.CreateChooser(intentShareFile, "Share File"), 43);

            var fileType = fileTypeFilter[0];

            var intent = new Intent(Intent.ActionCreateDocument);
            intent.AddCategory(Intent.CategoryOpenable);
            intent.SetType(GetMimeType(fileType));

            intent.SetFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission
                            | ActivityFlags.GrantPersistableUriPermission
                            | ActivityFlags.GrantPrefixUriPermission);

            if (fileType == ".pix2d")
            {
                defaultFileName += fileType;
            }

            intent.PutExtra(Intent.ExtraTitle, defaultFileName);
            MainActivity.Instance.StartActivityForResult(Intent.CreateChooser(intent, "Select Save Location"), 43);
            _tcs = new AccessFileTcs(fileTypeFilter[0], true);
            return _tcs.Task;
        }

        private static void TakePermissions(ContentResolver resolver, Android.Net.Uri uri)
        {
            resolver.TakePersistableUriPermission(uri,
                ActivityFlags.GrantReadUriPermission
                | ActivityFlags.GrantWriteUriPermission);
        }
        public override async Task<List<IFileContentSource>> GetMruFilesAsync()
        {
            try
            {
                var status = await Xamarin.Essentials.Permissions.RequestAsync<Xamarin.Essentials.Permissions.StorageRead>();

                var settingService = DefaultServiceLocator.ServiceLocatorProvider().GetInstance<ISettingsService>();
                var mruList = settingService.Get<HashSet<MruRecord>>("mru")?.ToHashSet();

                return mruList?
                           .Where(x => !string.IsNullOrWhiteSpace(x.Path) && !string.IsNullOrWhiteSpace(x.Name))
                           .Select(x =>
                {
                    return new AndroidFileContentSource(Path.GetExtension(x.Name), Android.Net.Uri.Parse(x.Path))
                    {
                        Title = x.Name
                    } as IFileContentSource;
                }).ToList() ?? new List<IFileContentSource>();
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
            return new List<IFileContentSource>();
        }

        private string GetMimeType(string fileType)
        {
            switch (fileType)
            {
                case ".png": return "image/png";
                case ".jpg": return "image/jpg";
                case ".gif": return "image/gif";
                case ".pix2d": return "application/pix2d";
                default:
                    throw new NotImplementedException();
            }
        }
    }
}
