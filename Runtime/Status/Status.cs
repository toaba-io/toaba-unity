using System;

namespace Toaba
{
    /// <summary>The status of the app set in Project Settings → Toaba.</summary>
    public static class Status
    {
        static StatusService Service => ToabaClient.Shared.Status;

        /// <inheritdoc cref="StatusService.InMaintenance"/>
        public static bool InMaintenance => Service.InMaintenance;

        /// <inheritdoc cref="StatusService.MaintenanceChanged"/>
        public static event Action<bool> MaintenanceChanged
        {
            add => Service.MaintenanceChanged += value;
            remove => Service.MaintenanceChanged -= value;
        }
    }
}
