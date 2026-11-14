using System;
using System.Collections.Generic;
using AsiErp.Models;

namespace AsiErp.Services
{
    public static class SeedData
    {
        // Sample data permanently disabled - all data comes strictly from Cloud Firestore DB
        public static List<RawMaterialItem> CreateRawMaterials() => new();
        public static List<GeneralItem> CreateGeneralItems() => new();
        public static List<ProductItem> CreateProducts() => new();
        public static void PopulateTransactions(AppDataService ds) { }
    }
}
