using Flylet.Controls;
using Flylet.Core.Display;
using Flylet.Helpers;

namespace Flylet
{
    public class BrightnessFlyoutHelper : FlyoutHelperBase
    {
        private BrightnessControl brightnessControl;

        public BrightnessFlyoutHelper()
        {
            Initialize();
        }

        public void Initialize()
        {
            AlwaysHandleDefaultFlyout = true;

            BrightnessManager.Initialize();

            brightnessControl = new BrightnessControl();

            PrimaryContent = brightnessControl;

            OnEnabled();
        }

        public override bool CanHandleNativeOnScreenFlyout(FlyoutTriggerData triggerData)
        {
            if (triggerData.TriggerType == FlyoutTriggerType.Brightness)
                return true;

            return base.CanHandleNativeOnScreenFlyout(triggerData);
        }

        public override void OnFlyoutOpening()
        {
            BrightnessManager.RefreshExternalBrightness();
        }

        protected override void OnEnabled()
        {
            base.OnEnabled();

            AppDataHelper.BrightnessModuleEnabled = IsEnabled;

            BrightnessManager.Initialize();
        }

        protected override void OnDisabled()
        {
            base.OnDisabled();

            BrightnessManager.Suspend();

            AppDataHelper.BrightnessModuleEnabled = IsEnabled;
        }
    }
}
