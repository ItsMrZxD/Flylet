using Flylet.Controls;
using Flylet.Core.Utilities;
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

        /// <summary>
        /// Whether this PC has any Wi-Fi or Bluetooth hardware. When false, the module has
        /// nothing to control, so the settings page disables its toggle instead of leaving a dead one.
        /// </summary>
        public bool HasRadios { get; private set; } = true;

        #endregion

        public AirplaneModeFlyoutHelper()
        {
            Initialize();
        }

        public void Initialize()
        {
            AlwaysHandleDefaultFlyout = true;

            HasRadios = RadioAvailability.HasAnyRadios();

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
