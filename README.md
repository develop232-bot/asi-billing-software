# ASI ERP · Afrah Samiya Industries Pvt Ltd
### C# Desktop Software (WPF .NET 10 + Google Cloud Firestore)

This software converts the **ASI ERP (Afrah Samiya Industries)** HTML system into a full-featured, professional C# desktop application built in the same architecture as **AgentFrontend** (WPF MVVM, .NET 10, Google Cloud Firestore).

---

## 📁 Project Location
- **Path:** `C:\Users\DELL\source\repos\AsiErp`
- **Solution:** `AsiErp.slnx`
- **Project File:** `AsiErp.csproj`
- **Executable:** `bin\Debug\net10.0-windows\AsiErp.exe`

---

## 🔑 Firebase Cloud Firestore Setup ("Replace the DB file")

The application uses Google Cloud Firestore for its cloud database.

1. Go to your **Firebase Console** -> **Project Settings** -> **Service Accounts**.
2. Click **Generate new private key** (downloads a `.json` file).
3. Rename or copy this file to:
   ```text
   C:\Users\DELL\source\repos\AsiErp\firebase-key.json
   ```
4. When you rebuild or run the app, `firebase-key.json` is automatically copied to the output folder.
5. The application will immediately detect the `project_id`, authenticate with Firebase, and synchronize your data:
   - `products` (finished shoes, uniforms, readymade goods)
   - `raw_materials` (leather, soles, thread, fabrics, trims)
   - `general_items` (boxes, covers, bags, tape)
   - `sales` (retail bills, domestic B2B invoices, overseas exports)
   - `purchases` (vendor bills for raw material, readymade, and packaging)
   - `work_orders` (shop floor production stages & BOM consumption)
   - `expenses` (salary, electricity, water, rent, daily operational expenses)
   - `investments` (promoter capital & bank term loans)

> **Note on Local Mode:** If `firebase-key.json` contains the placeholder key or is offline, the application seamlessly runs in **Local Data Mode** using local persistence (`asi_erp_state.json`) with the complete sample data loaded, so you can test all screens, billing, inventory, and reports right away without errors!

---

## 🌟 Modules & Features

### 1. 📊 Dashboard
- **6 Real-Time KPI Cards:** Today's Retail Sales, Domestic + Export Sales, MTD Total Sales, Outstanding to Receive, Vendor Payments to Pay, Low Stock Alerts.
- **Sales & Expense Chart:** 6-month stacked overview comparing monthly revenue by channel against expenses.
- **Top Selling Products:** Sales distribution breakdown with percentage shares.
- **Shop Floor Pipeline:** Real-time unit counters across 7 manufacturing stages: *Pending → Cutting → Stitching → Lasting & pressing → Finishing → Packing → Completed*.
- **Quick Action Buttons:** Fast shortcuts to create bills, invoices, work orders, products, and payments.

### 2. 🛒 Retail Billing (Point of Sale / POS)
- **High-Speed Counter:** Filter shoes, uniforms, and accessories with instant search and stock count indicators.
- **Smart Bulk Rates:** Automatically switches to discounted bulk pricing when quantity is 10 or more.
- **Interactive Cart:** Size selectors, quantity increment/decrement, percentage discount, and GST breakdown.
- **Direct Checkout:** Records payment mode (Cash, UPI, Card), location (Showroom, Plant, Exhibition), captures customer details, deducts stock, and generates a printable receipt.

### 3. 🧾 Sales & Invoicing Hub
- **Domestic Tax Invoices:** Intra-state (CGST + SGST) vs Inter-state (IGST), customer GSTIN, place of supply, vehicle number, and credit periods.
- **Overseas Export Invoices:** Multi-currency support (AED, SAR, QAR, OMR, KWD, BHD, USD), live exchange rate, Incoterms (FOB, CIF, CFR, EXW), ports, and zero-rated GST under LUT.
- **Outstanding Ledger:** Track paid, partial, and unpaid balances with 1-click payment collection.

### 4. 🛍️ Purchase Management
- **Three Dedicated Registers:**
  - *Raw Material:* Leather, soles, fabrics, thread, glue, steel toes.
  - *Readymade Products:* Slippers, socks, gloves, belts purchased for resale.
  - *General Packaging:* Shoe boxes, uniform poly covers, customer bags, master cartons.
- **Automated Bookkeeping:** Automatically increments inventory and logs purchase bills as Accounts Expenses.

### 5. 🏭 Manufacturing & Work Orders
- **Status Lifecycle:** Tracks work orders through shop floor stages with promised due dates and overdue alerts.
- **BOM Recipe Suggester:** Click **"Suggest from Recipe"** to automatically calculate raw materials needed based on product BOMs.
- **Inventory Integration:** Deducts raw materials upon issuance and automatically adds finished goods to warehouse stock upon completion.

### 6. 📦 Inventory / Stock Control
- **4 Categorized Views:** Raw Materials, Finished Goods, Readymade Resale, and General Packaging.
- **Stock Valuation:** Total inventory worth calculated at cost/purchase rates.
- **Visual Level Meters:** Status pills (*In stock*, *Low*, *Out*) with color-coded reorder bars.
- **Manual Adjustments:** Add or remove stock with audit notes for samples, damages, or opening balances.

### 7. 🏷️ Products & BOM Recipes
- **Unified Catalog:** Manage manufactured goods, resale products, raw materials, and packing items in one place.
- **GST 2.0 Engine:** Automatically applies 5% GST for prices up to ₹2,500 and 18% above ₹2,500 for footwear & apparel.
- **Multi-Tier Pricing:** Retail MRP, Bulk (10+ units), Domestic Wholesale, and Export USD prices.
- **BOM Recipe Builder:** Define materials and quantities consumed per finished unit.

### 8. 💼 Accounts & Financial Statements
- **Income Register:** Taxable revenue tracking without GST.
- **Expense Register:** Daily running expenses, monthly utilities (Salary, EB, Water, Rent, ESI/PF), yearly licences, and auto-linked purchase bills.
- **Capital & ROI Recovery:** Total invested capital vs. cumulative net profit recovery meter.
- **Profit & Loss Statement:** Real-time T-Account statement comparing expenses by category against revenue by channel, plus month-by-month profit margins.

### 9. 🖨️ Document Viewer & Printing
- Formatted invoices and purchase bills with company letterhead, tax tables, GSTIN, and payment terms.
- Built-in **"Print / Save as PDF"** button using WPF `PrintDialog`.

---

## 🚀 How to Run
In PowerShell or Visual Studio:
```powershell
cd C:\Users\DELL\source\repos\AsiErp
dotnet run
```
Or open `AsiErp.slnx` in Visual Studio and press **F5**.
