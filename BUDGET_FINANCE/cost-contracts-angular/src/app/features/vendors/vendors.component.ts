import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DataService } from '../../core/services/data.service';
import { KpiTileComponent } from '../../shared/kpi-tile.component';
import { UiCardComponent } from '../../shared/ui-card.component';
import { StatusSymbolComponent } from '../../shared/status-symbol.component';
import { DrillDrawerComponent } from '../../shared/drill-drawer.component';
import { CrPipe, InrPipe } from '../../core/pipes/format.pipes';
import { VendorPerf } from '../../core/models/models';
@Component({
  selector: 'cc-vendors', standalone: true,
  imports: [CommonModule, KpiTileComponent, UiCardComponent, StatusSymbolComponent, DrillDrawerComponent, CrPipe, InrPipe],
  template: `

<!-- <div class="kpi-row">
    <div class="kpi navy"><div class="k-lbl">Vendors (entity)</div>
    <div class="k-val">24</div>
    <div class="k-sub">Across all projects</div>
    </div>
    <div class="kpi good"><div class="k-lbl">On vendor portal</div>
    <div class="k-val">5</div>
    <div class="k-sub">Govt. Office mapped</div>
    </div>
    <div class="kpi "><div class="k-lbl">Contractor WO value</div>
    <div class="k-val">₹190.47 <span class="u">Cr</span></div>
    <div class="k-sub">Govt. Office</div>
    </div>
    <div class="kpi violet"><div class="k-lbl">Retention held</div>
    <div class="k-val">₹5.78 <span class="u">Cr</span></div>
    <div class="k-sub">Entity total</div>
    </div>
  </div> -->

   <div class="kpi-row" *ngIf="ds.scope() === 'entity'">
    <cc-kpi [compact]="true" [sub]="'Across all projects'" cls="navy" label="Vendors (entity)" [value]="rows().length"></cc-kpi>
    <cc-kpi [compact]="true" [sub]="'Govt. Office mapped'" label="On vendor portal" [value]="'₹'+(orderVal()|cr)" unit="Cr"></cc-kpi>
    <cc-kpi [compact]="true" [sub]="'Govt. Office'" cls="good" label="Contractor WO value" [value]="'₹'+(billed()|cr)" unit="Cr"></cc-kpi>
    <cc-kpi [compact]="true" [sub]="'Entity total'" cls="violet" label="Retention held" [value]="'₹'+(ret()|cr)" unit="Cr"></cc-kpi>
  </div>
  <div class="card"><div class="card-h"><h3>Vendors — consolidated</h3><span class="hint">Shared vendors bill across projects · project column shows engagement</span></div>
  
  <div class="tbl-scroll" *ngIf="ds.scope() === 'entity'"><table><thead><tr><th>Vendor</th><th>Category</th><th>Project</th><th>Portal</th><th class="num">WO Value</th><th class="num">Billed</th><th class="num">Retention</th></tr></thead>
    <tbody><tr class="click" onclick="drillVendor(0)">
    <td class="strong">Quality Infraconstruction LLP<div class="mut" style="font-weight:400;font-size:10.5px">Civil Core &amp; Shell</div></td>
    <td><span class="stat st-appr">Contractor</span></td>
    <td class="mut">Govt. Office</td>
    <td><span class="stat st-paid">On Portal</span></td>
    <td class="num">₹152.27</td><td class="num">₹24.05</td><td class="num">₹0.78</td>
  </tr><tr class="click" onclick="drillVendor(1)">
    <td class="strong">Deepak &amp; Sahil Engcon Pvt Ltd<div class="mut" style="font-weight:400;font-size:10.5px">Civil Sub-work</div></td>
    <td><span class="stat st-appr">Contractor</span></td>
    <td class="mut">Govt. Office</td>
    <td><span class="stat st-paid">On Portal</span></td>
    <td class="num">₹38.2</td><td class="num">₹5.9</td><td class="num">₹2.46</td>
  </tr><tr class="click" onclick="drillVendor(2)">
    <td class="strong">Shri Khatu Shyam Alloys<div class="mut" style="font-weight:400;font-size:10.5px">TMT Reinf. Steel</div></td>
    <td><span class="stat st-pend">Material</span></td>
    <td class="mut">Govt. Office</td>
    <td><span class="stat st-paid">On Portal</span></td>
    <td class="num">—</td><td class="num">₹8.05</td><td class="num">₹0</td>
  </tr><tr class="click" onclick="drillVendor(3)">
    <td class="strong">Guardian Castings Pvt Ltd<div class="mut" style="font-weight:400;font-size:10.5px">Steel</div></td>
    <td><span class="stat st-pend">Material</span></td>
    <td class="mut">Govt. Office</td>
    <td><span class="stat st-paid">On Portal</span></td>
    <td class="num">—</td><td class="num">₹2.16</td><td class="num">₹0</td>
  </tr><tr class="click" onclick="drillVendor(4)">
    <td class="strong">Trendz Exim<div class="mut" style="font-weight:400;font-size:10.5px">TMT Bars (debit contra)</div></td>
    <td><span class="stat st-pend">Material</span></td>
    <td class="mut">Govt. Office</td>
    <td><span class="stat st-hold">Off Portal</span></td>
    <td class="num">—</td><td class="num">₹0.2</td><td class="num">₹0</td>
  </tr><tr class="click" onclick="drillVendor(5)">
    <td class="strong">Shree Sai Safety Net<div class="mut" style="font-weight:400;font-size:10.5px">Safety Net / MSME</div></td>
    <td><span class="stat st-pend">Material</span></td>
    <td class="mut">Govt. Office</td>
    <td><span class="stat st-paid">On Portal</span></td>
    <td class="num">—</td><td class="num">₹0.16</td><td class="num">₹0</td>
  </tr><tr class="click" onclick="drillVendor(6)">
    <td class="strong">Yogi Buildcon<div class="mut" style="font-weight:400;font-size:10.5px">Legacy Civil</div></td>
    <td><span class="stat st-appr">Contractor</span></td>
    <td class="mut">Govt. Office</td>
    <td><span class="stat st-hold">Off Portal</span></td>
    <td class="num">—</td><td class="num">₹0</td><td class="num">₹0.18</td>
  </tr></tbody></table></div></div>

  <div class="page-title" *ngIf="ds.scope() === 'project'">Contractors — <i>Vendor Performance</i></div>
  <div class="page-sub" *ngIf="ds.scope() === 'project'">Orders, billing, payment & retention by vendor · ₹ Cr</div>
  <div class="kpi-row" *ngIf="ds.scope() === 'project'">
    <cc-kpi [compact]="true" cls="navy" label="Vendors" [value]="rows().length"></cc-kpi>
    <cc-kpi [compact]="true" label="Order value" [value]="'₹'+(orderVal()|cr)" unit="Cr"></cc-kpi>
    <cc-kpi [compact]="true" cls="good" label="Billed" [value]="'₹'+(billed()|cr)" unit="Cr"></cc-kpi>
    <cc-kpi [compact]="true" cls="violet" label="Retention" [value]="'₹'+(ret()|cr)" unit="Cr"></cc-kpi>
  </div>
  <cc-card title="Vendor performance" hint="₹ Cr · click a vendor to drill"  *ngIf="ds.scope() === 'project'">
    <table class="compact">
      <thead><tr><th>Vendor</th><th>Category</th><th class="num">Orders</th><th class="num">Order Value</th>
        <th class="num">Billed</th><th class="num">Paid</th><th class="num">Balance</th><th class="num">Invoices</th>
        <th class="num">Retention</th><th class="num">GRN</th><th class="num">Bill %</th></tr></thead>
      <tbody>
        <tr class="click" *ngFor="let v of rows()" (click)="sel.set(v); tab.set('Performance')">
          <td class="strong"><span class="trunc w160" [title]="v.name">{{v.name}}</span></td>
          <td><span class="stat st-open">{{v.cat}}</span></td>
          <td class="num">{{v.orders}}</td><td class="num strong">{{v.orderValue|cr}}</td>
          <td class="num">{{v.billed|cr}}</td><td class="num">{{v.paid|cr}}</td>
          <td class="num" [class.neg]="v.balance>0">{{v.balance|cr}}</td>
          <td class="num">{{v.invoices}}</td><td class="num">{{v.retention|cr}}</td><td class="num">{{v.grn}}</td>
          <td class="num strong">{{v.billProgress}}%</td>
        </tr>
      </tbody>
    </table>
  </cc-card>

  <cc-drawer [open]="!!sel()" eyebrow="Vendor" [title]="sel()?.name||''"
             [subtitle]="(sel()?.cat||'')+' · '+(sel()?.orders||0)+' orders'"
             [tabs]="['Performance','Orders','Invoices']" [active]="tab()" (pick)="tab.set($any($event))" (close)="sel.set(null)">
    <ng-container *ngIf="sel() as v">
      <ng-container *ngIf="tab()==='Performance'">
        <div class="dr-kpis">
          <div class="dr-k"><div class="l">Order value</div><div class="v">₹{{v.orderValue|cr}} Cr</div></div>
          <div class="dr-k"><div class="l">Billed</div><div class="v">₹{{v.billed|cr}} Cr</div></div>
          <div class="dr-k"><div class="l">Bill %</div><div class="v">{{v.billProgress}}%</div></div>
        </div>
        <table class="kv-tbl">
          <tr><td>Paid</td><td>₹{{v.paid|cr}} Cr</td></tr>
          <tr><td>Balance</td><td>₹{{v.balance|cr}} Cr</td></tr>
          <tr><td>Invoices</td><td>{{v.invoices}} ({{v.apprPct}}% approved, {{v.rejBills}} rejected)</td></tr>
          <tr><td>Retention held</td><td>₹{{v.retention|cr}} Cr</td></tr>
          <tr><td>GRN / receipts</td><td>{{v.grn}}</td></tr>
        </table>
      </ng-container>
      <ng-container *ngIf="tab()==='Orders'">
        <div class="sec-lbl" *ngIf="vWos(v).length">Work Orders</div>
        <table class="compact" *ngIf="vWos(v).length">
          <thead><tr><th>WO No</th><th>GL Date</th><th>Dept</th><th class="num">Amount (₹Cr)</th><th class="ctr">Appr</th><th class="ctr">Bill</th></tr></thead>
          <tbody><tr *ngFor="let w of vWos(v)"><td class="mut">{{w.no}}</td><td class="mut">{{w.glDate}}</td>
            <td><span class="trunc w120">{{w.dept}}</span></td><td class="num strong">{{w.amount|cr}}</td>
            <td class="ctr"><cc-sym kind="approval" [value]="w.appr"></cc-sym></td>
            <td class="ctr"><cc-sym kind="billing" [value]="w.billStatus"></cc-sym></td></tr></tbody>
        </table>
        <div class="sec-lbl" *ngIf="vPos(v).length" style="margin-top:12px">Purchase Orders</div>
        <table class="compact" *ngIf="vPos(v).length">
          <thead><tr><th>PO No</th><th>Cat</th><th>GL Date</th><th class="num">Amount (₹L)</th><th class="ctr">Appr</th></tr></thead>
          <tbody><tr *ngFor="let p of vPos(v)"><td class="mut">{{p.no}}</td><td>{{p.cat}}</td><td class="mut">{{p.glDate}}</td>
            <td class="num strong">{{p.val|cr}}</td><td class="ctr"><cc-sym kind="approval" [value]="p.appr"></cc-sym></td></tr></tbody>
        </table>
        <div class="note" *ngIf="!vWos(v).length && !vPos(v).length">No PO/WO on record (billed without order).</div>
      </ng-container>
      <ng-container *ngIf="tab()==='Invoices'">
        <table class="compact" *ngIf="vInv(v).length; else ni">
          <thead><tr><th>Invoice No</th><th>GL Date</th><th>Dept</th><th class="ctr">Appr</th><th class="ctr">Pay</th>
            <th class="num">Bill Amount</th><th class="num">Paid Amt</th><th class="num">Balance Amt</th></tr></thead>
          <tbody><tr *ngFor="let i of vInv(v)"><td class="mut">{{i.no}}</td><td class="mut">{{i.glDate}}</td>
            <td><span class="trunc w120">{{i.dept}}</span></td>
            <td class="ctr"><cc-sym kind="approval" [value]="i.appr"></cc-sym></td>
            <td class="ctr"><cc-sym kind="payment" [value]="i.payStatus"></cc-sym></td>
            <td class="num strong">{{i.amt|inr}}</td><td class="num">{{i.paid|inr}}</td>
            <td class="num" [class.neg]="i.balance>0">{{i.balance|inr}}</td></tr></tbody>
        </table><ng-template #ni><div class="note">No invoices for this vendor.</div></ng-template>
      </ng-container>
    </ng-container>
  </cc-drawer>`,
  styles: [`.dr-kpis{display:flex;gap:10px;margin-bottom:12px}.dr-k{flex:1;background:var(--bg);border:1px solid var(--line);border-radius:10px;padding:9px 11px}
  .dr-k .l{font-size:10px;color:var(--t3);text-transform:uppercase}.dr-k .v{font-family:var(--serif);font-size:19px}
  .kv-tbl{width:100%;font-size:12.5px}.kv-tbl td{padding:6px 4px;border-bottom:1px solid var(--line)}.kv-tbl td:first-child{color:var(--t2);width:38%}`]
})
export class VendorsComponent {
  ds = inject(DataService);
  sel = signal<VendorPerf | null>(null); tab = signal<'Performance' | 'Orders' | 'Invoices'>('Performance');
  rows = computed(() => this.ds.current()?.vendorPerf ?? []);
  orderVal = computed(() => this.rows().reduce((s, v) => s + v.orderValue, 0));
  billed = computed(() => this.rows().reduce((s, v) => s + v.billed, 0));
  ret = computed(() => this.rows().reduce((s, v) => s + v.retention, 0));
  vWos(v: VendorPerf) { return (this.ds.current()?.wos ?? []).filter(w => w.vendor === v.name); }
  vPos(v: VendorPerf) { return (this.ds.current()?.pos ?? []).filter(p => p.supplier === v.name); }
  vInv(v: VendorPerf) { return this.ds.current()?.vendorInv[v.name] ?? []; }
}
