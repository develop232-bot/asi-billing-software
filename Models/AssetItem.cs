using System;
using Google.Cloud.Firestore;

namespace AsiErp.Models
{
    [FirestoreData]
    public class AssetItem
    {
        [FirestoreProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [FirestoreProperty("name")]
        public string Name { get; set; } = string.Empty;

        [FirestoreProperty("kind")]
        public string Kind { get; set; } = "Machinery"; // Machinery, Building, Vehicle, Furniture, Computer, Other

        [FirestoreProperty("date")]
        public string Date { get; set; } = DateTime.Now.ToString("yyyy-MM-dd");

        [FirestoreProperty("cost")]
        public double Cost { get; set; }

        [FirestoreProperty("rate")]
        public double DepreciationRate { get; set; } = 15.0; // % per year

        public double DepreciationSoFar
        {
            get
            {
                if (DateTime.TryParse(Date, out var dt))
                {
                    double days = Math.Max(0, (DateTime.Today - dt.Date).TotalDays);
                    return Math.Min(Cost, Cost * (DepreciationRate / 100.0) * (days / 365.0));
                }
                return 0;
            }
        }

        public double BookValue => Math.Max(0, Cost - DepreciationSoFar);
    }
}
