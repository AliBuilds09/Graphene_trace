using System.Collections.ObjectModel;
using MyProject.Models;

namespace MyProject.Controllers
{
    public static class AlertsController
    {
        public static ObservableCollection<Alert> Alerts { get; } = new ObservableCollection<Alert>();

        public static void AddAlert(int peakPressure, int threshold, string source)
        {
            var alert = new Alert
            {
                PeakPressure = peakPressure,
                Threshold = threshold,
                Source = source,
                Message = $"High Peak Pressure {peakPressure} (> {threshold})"
            };
            Alerts.Add(alert);
        }

        public static void Clear()
        {
            Alerts.Clear();
        }
    }
}