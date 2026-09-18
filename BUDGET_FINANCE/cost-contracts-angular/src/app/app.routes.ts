import { Routes } from '@angular/router';
export const routes: Routes = [
  { path:'', pathMatch:'full', redirectTo:'overview' },
  { path:'overview',        loadComponent:()=>import('./features/overview/overview.component').then(m=>m.OverviewComponent) },
  { path:'budget',          loadComponent:()=>import('./features/budget/budget.component').then(m=>m.BudgetComponent) },
  { path:'boq',             loadComponent:()=>import('./features/boq/boq.component').then(m=>m.BoqComponent) },
  { path:'vendors',         loadComponent:()=>import('./features/vendors/vendors.component').then(m=>m.VendorsComponent) },
  { path:'work-orders',     loadComponent:()=>import('./features/work-orders/work-orders.component').then(m=>m.WorkOrdersComponent) },
  { path:'purchase-orders', loadComponent:()=>import('./features/purchase-orders/purchase-orders.component').then(m=>m.PurchaseOrdersComponent) },
  { path:'asn-grn',         loadComponent:()=>import('./features/asn-grn/asn-grn.component').then(m=>m.AsnGrnComponent) },
  { path:'invoices',        loadComponent:()=>import('./features/invoices/invoices.component').then(m=>m.InvoicesComponent) },
  { path:'risk',            loadComponent:()=>import('./features/risk/risk.component').then(m=>m.RiskComponent) },
];
