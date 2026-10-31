using Flylet.Controls;
using Flylet.Core.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Media;
using WinRT;
using Comp = Windows.UI.Composition;

namespace Flylet.UI
{
    /// <summary>
    /// Real glass behind the flyout cards: a Windows.UI.Composition layer under the WPF content with one host
    /// backdrop (a blur of whatever is behind the window) per visible card, clipped to that card's shape.
    /// DWM's own blur (SetWindowCompositionAttribute) always fills the whole window, gaps between cards
    /// included, and ignores window regions, so it can't be used here.
    /// </summary>
    internal sealed class FlyoutGlass
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct DispatcherQueueOptions
        {
            public int dwSize;
            public int threadType;
            public int apartmentType;
        }

        [DllImport("CoreMessaging.dll")]
        private static extern int CreateDispatcherQueueController(DispatcherQueueOptions options, out IntPtr controller);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        // ICompositorDesktopInterop::CreateDesktopWindowTarget, the first method after IUnknown's three
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CreateDesktopWindowTargetFn(IntPtr self, IntPtr hwnd, int isTopmost, out IntPtr target);

        private const int DWMWA_USE_HOSTBACKDROPBRUSH = 17;
        private static readonly Guid ICompositorDesktopInteropId = new("29E691FA-4567-4DCA-B319-D0F207EB6807");

        // A Compositor needs a DispatcherQueue on its thread for as long as it lives
        private static IntPtr dispatcherQueueController;

        private readonly FlyoutWindow window;
        private readonly UIManager uiManager;

        private Comp.Compositor compositor;
        private Comp.Desktop.DesktopWindowTarget target;
        private Comp.ContainerVisual root;
        private Comp.CompositionBrush backdropBrush;
        private bool failed;

        private readonly List<FlyoutBackgroundLayer> layers = new();
        private readonly List<(Rect Bounds, double Radius)> cards = new();
        private readonly StringBuilder shapeKey = new();
        private string lastShapeKey;
        private bool layersDirty = true;
        private bool rendering;

        public FlyoutGlass(FlyoutWindow window, UIManager uiManager)
        {
            this.window = window;
            this.uiManager = uiManager;

            // A card showing, hiding or resizing: find the cards again on the next frame
            window.LayoutUpdated += (_, _) => layersDirty = true;
            window.IsVisibleChanged += (_, _) => UpdateRendering();
            uiManager.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(UIManager.IsGlassActive))
                {
                    UpdateRendering();
                }
            };
            UpdateRendering();
        }

        /// <summary>
        /// Follows the cards every frame while the flyout is shown, so the glass moves and fades with the
        /// open/close animations, which change render transforms and opacity rather than layout.
        /// </summary>
        private void UpdateRendering()
        {
            bool active = uiManager.IsGlassActive && window.IsVisible && !failed;
            if (active == rendering)
                return;

            rendering = active;
            if (active)
            {
                layersDirty = true;
                CompositionTarget.Rendering += OnRendering;
            }
            else
            {
                CompositionTarget.Rendering -= OnRendering;
                if (root != null)
                {
                    root.IsVisible = false;
                }
            }
        }

        private void OnRendering(object sender, EventArgs e)
        {
            if (!EnsureLayer())
            {
                UpdateRendering();
                return;
            }

            if (layersDirty)
            {
                layersDirty = false;
                layers.Clear();
                FindLayers(window);
            }

            double dpi = VisualTreeHelper.GetDpi(window).DpiScaleX;
            // The open/close slide moves the whole window content through its own render transform,
            // which TransformToAncestor(window) leaves out
            var slide = window.RenderTransform ?? Transform.Identity;

            cards.Clear();
            shapeKey.Clear().Append(dpi);
            foreach (var layer in layers)
            {
                if (!layer.IsVisible || layer.ActualWidth <= 0 || !layer.IsDescendantOf(window))
                    continue;

                var bounds = slide.TransformBounds(layer.TransformToAncestor(window).TransformBounds(new Rect(layer.RenderSize)));
                double radius = layer.CornerRadius.TopLeft;
                cards.Add((bounds, radius));
                shapeKey.Append(';').Append(bounds.X).Append(',').Append(bounds.Y).Append(',')
                    .Append(bounds.Width).Append(',').Append(bounds.Height).Append(',').Append(radius);
            }

            string key = shapeKey.ToString();
            if (key != lastShapeKey)
            {
                lastShapeKey = key;
                UpdateSprites(dpi);
            }

            root.Opacity = (float)window.Opacity;
            root.IsVisible = true;
        }

        private void FindLayers(DependencyObject parent)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is FlyoutBackgroundLayer layer)
                {
                    layers.Add(layer);
                }
                else
                {
                    FindLayers(child);
                }
            }
        }

        private void UpdateSprites(double dpi)
        {
            root.Children.RemoveAll();
            foreach (var (bounds, radius) in cards)
            {
                var size = new Vector2((float)(bounds.Width * dpi), (float)(bounds.Height * dpi));
                var geometry = compositor.CreateRoundedRectangleGeometry();
                geometry.Size = size;
                geometry.CornerRadius = new Vector2((float)(radius * dpi));

                var sprite = compositor.CreateSpriteVisual();
                sprite.Brush = backdropBrush;
                sprite.Offset = new Vector3((float)(bounds.X * dpi), (float)(bounds.Y * dpi), 0);
                sprite.Size = size;
                sprite.Clip = compositor.CreateGeometricClip(geometry);
                root.Children.InsertAtTop(sprite);
            }
        }

        private bool EnsureLayer()
        {
            if (root != null)
                return true;
            if (failed || window.Handle == IntPtr.Zero)
                return false;

            try
            {
                if (global::Windows.System.DispatcherQueue.GetForCurrentThread() == null)
                {
                    var options = new DispatcherQueueOptions
                    {
                        dwSize = Marshal.SizeOf<DispatcherQueueOptions>(),
                        threadType = 2,     // DQTYPE_THREAD_CURRENT
                        apartmentType = 2   // DQTAT_COM_STA
                    };
                    Marshal.ThrowExceptionForHR(CreateDispatcherQueueController(options, out dispatcherQueueController));
                }

                // Without this, a desktop app's host backdrop brush draws nothing
                int on = 1;
                Marshal.ThrowExceptionForHR(DwmSetWindowAttribute(window.Handle, DWMWA_USE_HOSTBACKDROPBRUSH, ref on, sizeof(int)));

                compositor = new Comp.Compositor();

                IntPtr compositorUnknown = ((IWinRTObject)compositor).NativeObject.ThisPtr;
                Guid interopId = ICompositorDesktopInteropId;
                Marshal.ThrowExceptionForHR(Marshal.QueryInterface(compositorUnknown, ref interopId, out IntPtr interop));
                try
                {
                    var createTarget = Marshal.GetDelegateForFunctionPointer<CreateDesktopWindowTargetFn>(
                        Marshal.ReadIntPtr(Marshal.ReadIntPtr(interop), 3 * IntPtr.Size));
                    // Not topmost: the glass goes under the WPF content
                    Marshal.ThrowExceptionForHR(createTarget(interop, window.Handle, 0, out IntPtr targetPointer));
                    target = Comp.Desktop.DesktopWindowTarget.FromAbi(targetPointer);
                    Marshal.Release(targetPointer);
                }
                finally
                {
                    Marshal.Release(interop);
                }

                backdropBrush = compositor.CreateHostBackdropBrush();
                root = compositor.CreateContainerVisual();
                target.Root = root;
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{nameof(FlyoutGlass)}: glass unavailable: {ex.Message}");
                failed = true;
                return false;
            }
        }
    }
}
