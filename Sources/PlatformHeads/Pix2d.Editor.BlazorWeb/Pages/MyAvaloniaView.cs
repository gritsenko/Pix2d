using System.Reflection;
using Avalonia.Web.Blazor;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Pix2d.BlazorWasm.Pages
{
    public class MyAvaloniaView : AvaloniaView
    {
        [Inject] private IJSRuntime Js { get; set; } = null!;

        protected override void OnAfterRender(bool firstRender)
        {
            base.OnAfterRender(firstRender);

            if(firstRender)
                HideSplash();
        }

        private async void HideSplash()
        {
            while (!IsInitialized())
            {
                await Task.Delay(300);
            }
            await Task.Delay(300);
            await Js.InvokeVoidAsync("hideSplash");
        }

        private bool IsInitialized()
        {
            var field = typeof(AvaloniaView).GetField("_initialised", BindingFlags.NonPublic | BindingFlags.Instance);
            return (bool)field.GetValue(this);
        }
    }
}
