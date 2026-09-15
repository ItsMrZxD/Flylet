using Flylet.Controls;
using Flylet.Helpers;

namespace Flylet
{
    public class AirplaneModeFlyoutHelper : FlyoutHelperBase
    {
        private AirplaneModeControl airplaneModeControl;

        #region Properties

        private bool airplaneMode;

        public bool AirplaneMode
        {
            get => airplaneMode;
            private set => SetProperty(ref airplaneMode, value);
        }

        #endregion

        public AirplaneModeFlyoutHelper()
        {
            Initialize();
        }

        public void Initialize()
        {
            AlwaysHandleDefaultFlyout = true;

            airplaneModeControl = new AirplaneModeControl();

            PrimaryContent = airplaneModeControl;

            OnEnabled();
        }

        public override bool CanHandleNativeOnScreenFlyout(FlyoutTriggerData triggerData)
        {
            if (triggerData.TriggerType == FlyoutTriggerType.AirplaneMode)
            {
                AirplaneMode = triggerData.Data is bool isEnabled && isEnabled;
                return true;
            }

            return base.CanHandleNativeOnScreenFlyout(triggerData);
        }

        protected override void OnEnabled()
        {
            base.OnEnabled();

            AppDataHelper.AirplaneModeModuleEnabled = IsEnabled;
        }

        protected override void OnDisabled()
        {
            base.OnDisabled();

            AppDataHelper.AirplaneModeModuleEnabled = IsEnabled;
        }
    }
}
