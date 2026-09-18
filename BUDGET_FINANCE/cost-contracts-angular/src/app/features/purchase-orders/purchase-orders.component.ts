import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DataService } from '../../core/services/data.service';
import { KpiTileComponent } from '../../shared/kpi-tile.component';
import { UiCardComponent } from '../../shared/ui-card.component';
import { StatusSymbolComponent } from '../../shared/status-symbol.component';
import { SubTabsComponent } from '../../shared/sub-tabs.component';
import { DrillDrawerComponent } from '../../shared/drill-drawer.component';
import { BackBarComponent } from '../../shared/back-bar.component';
import { CrPipe, InrPipe } from '../../core/pipes/format.pipes';
import { PurchaseOrder, AsnHeader } from '../../core/models/models';
@Component({
  selector:'cc-purchase-orders', standalone:true,
  imports:[CommonModule, KpiTileComponent, UiCardComponent, StatusSymbolComponent, SubTabsComponent,
           DrillDrawerComponent, BackBarComponent, CrPipe, InrPipe],
  template:`
  <div class="page-title">Purchase <i>Orders</i></div>
  <div class="page-sub">Material & department procurement · GRN, approvals & retention · ₹ Lakh</div>
  <div class="kpi-row">
    <cc-kpi cls="navy" label="Purchase Orders" [value]="pos().length" [sub]="mat().length+' material · '+dept().length+' dept'"></cc-kpi>
    <cc-kpi label="Material value" [value]="'₹'+(matVal()|cr)" unit="Cr"></cc-kpi>
    <cc-kpi label="Department value" [value]="'₹'+(deptVal()|cr)" unit="Cr"></cc-kpi>
    <cc-kpi cls="good" label="Approved" [value]="approved()" [sub]="'of '+pos().length"></cc-kpi>
  </div>
  <cc-subtabs [tabs]="['Material','Department']" [active]="sub()" (pick)="sub.set($any($event))"></cc-subtabs>
  <cc-card [title]="sub()+' purchase orders'" hint="₹ Lakh · click PO No for lines / GRN">
    <table class="compact">
      <thead><tr><th>PO No</th><th>GL<span class="cc">Date</span></th><th>Department</th><th>Supplier</th>
        <th class="num">Amount<span class="cc">(₹L)</span></th><th class="num">Ret<span class="cc">%</span></th>
        <th class="ctr">Status</th><th class="ctr">Appr</th><th class="ctr"># GRN</th>
        <th>Last GRN<span class="cc">No</span></th><th>GRN<span class="cc">Date</span></th><th class="ctr">GRN<span class="cc">Status</span></th></tr></thead>
      <tbody>
        <tr class="click" *ngFor="let p of rows()" (click)="open(p)">
          <td><span class="amt-link">{{p.no}}</span></td><td class="mut">{{p.glDate}}</td>
          <td><span class="trunc w120" [title]="p.dept">{{p.dept}}</span></td>
          <td><span class="trunc w120" [title]="p.supplier">{{p.supplier}}</span></td>
          <td class="num strong">{{p.val|cr}}</td><td class="num">{{p.ret||0}}%</td>
          <td class="ctr"><cc-sym kind="billing" [value]="p.status"></cc-sym></td>
          <td class="ctr"><cc-sym kind="approval" [value]="p.appr"></cc-sym></td>
          <td class="ctr">{{ grnCount(p.no) || '—' }}</td>
          <td class="mut">{{p.grnNo||'—'}}</td><td class="mut">{{p.grnDate||'—'}}</td>
          <td class="ctr"><cc-sym *ngIf="p.grnNo" kind="asn" [value]="p.grnStatus"></cc-sym><span *ngIf="!p.grnNo">—</span></td>
        </tr>
      </tbody>
    </table>
  </cc-card>

  <cc-drawer [open]="!!sel()" eyebrow="Purchase Order" [title]="sel()?.no||''"
             [subtitle]="(sel()?.supplier||'')+' · '+(sel()?.cat||'')"
             [tabs]="['Summary','Item Lines','GRN Details']" [active]="tab()"
             (pick)="tab.set($any($event)); grnView.set('headers')" (close)="sel.set(null)">
    <ng-container *ngIf="sel() as p">
      <ng-container *ngIf="tab()==='Summary'">
        <table class="kv-tbl">
          <tr><td>Supplier</td><td>{{p.supplier}}</td></tr>
          <tr><td>Category</td><td>{{p.cat}}</td></tr>
          <tr><td>Amount</td><td>₹{{p.val|cr}} Lakh</td></tr>
          <tr><td>GL Date</td><td>{{p.glDate}}</td></tr>
          <tr><td>Department</td><td>{{p.dept}}</td></tr>
          <tr><td>Status</td><td>{{p.status}}</td></tr>
          <tr><td>Approval</td><td>{{p.appr}}</td></tr>
          <tr><td>Last GRN</td><td>{{p.grnNo||'—'}}</td></tr>
        </table>
      </ng-container>
      <ng-container *ngIf="tab()==='Item Lines'">
        <table class="compact" *ngIf="lines().length; else nl">
          <thead><tr><th>Item</th><th>Description</th><th class="num">Qty</th><th class="num">Rate</th>
            <th class="num">Amount</th><th class="num">Billed Qty</th><th>Code</th></tr></thead>
          <tbody><tr *ngFor="let l of lines()">
            <td class="mut">{{l.item}}</td><td><span class="trunc w220" [title]="l.desc">{{l.desc}}</span></td>
            <td class="num">{{l.qty|inr}}</td><td class="num">{{l.rate|inr}}</td><td class="num strong">{{l.amt|inr}}</td>
            <td class="num">{{l.bqty|inr}}</td><td class="mut">{{l.code}}<div class="sub">{{l.cdesc}}</div></td></tr></tbody>
        </table><ng-template #nl><div class="note">No item lines.</div></ng-template>
      </ng-container>
      <ng-container *ngIf="tab()==='GRN Details'">
        <ng-container *ngIf="grns(p.no).length; else ng">
          <ng-container *ngIf="grnView()==='headers'">
            <div class="sec-lbl">{{grns(p.no).length}} GRN(s) · click a GRN for lines</div>
            <table class="compact"><thead><tr><th>GRN No</th><th>Challan</th><th>Date</th>
              <th class="num">Qty</th><th class="num">Approved</th><th class="num">Billed</th><th class="ctr">Status</th></tr></thead>
              <tbody><tr class="click" *ngFor="let a of grns(p.no)" (click)="grnSel.set(a);grnView.set('lines')">
                <td><span class="amt-link">{{a.asnNo}}</span></td><td class="mut">{{a.challan}}</td><td class="mut">{{a.date}}</td>
                <td class="num">{{a.asnQty|inr}}</td><td class="num">{{a.apprQty|inr}}</td><td class="num">{{a.billQty|inr}}</td>
                <td class="ctr"><cc-sym kind="asn" [value]="a.status"></cc-sym></td></tr></tbody></table>
          </ng-container>
          <ng-container *ngIf="grnView()==='lines'">
            <cc-backbar label="GRN list" (back)="grnView.set('headers')"></cc-backbar>
            <div class="drill-head">{{grnSel()?.asnNo}} · {{grnSel()?.challan}}</div>
            <table class="compact"><thead><tr><th>Document No</th><th>Draft INV</th><th>Vendor Bill</th>
              <th class="num">Qty</th><th class="num">Approved</th><th class="num">Billed</th><th>Responsible</th></tr></thead>
              <tbody><tr *ngFor="let l of grnLines(grnSel()?.asnNo)">
                <td class="mut">{{l.docNo}}</td><td class="mut">{{l.draftInv||'—'}}</td><td class="mut">{{l.vendBill||'—'}}</td>
                <td class="num">{{l.asnQty|inr}}</td><td class="num">{{l.apprQty|inr}}</td><td class="num">{{l.billQty|inr}}</td>
                <td class="mut">{{l.responsible||'—'}}</td></tr></tbody></table>
          </ng-container>
        </ng-container><ng-template #ng><div class="note">No GRN raised against this PO yet.</div></ng-template>
      </ng-container>
    </ng-container>
  </cc-drawer>`,
  styles:[`.kv-tbl{width:100%;font-size:12.5px}.kv-tbl td{padding:6px 4px;border-bottom:1px solid var(--line)}.kv-tbl td:first-child{color:var(--t2);width:38%}`]
})
export class PurchaseOrdersComponent {
  ds=inject(DataService);
  sub=signal<'Material'|'Department'>('Material');
  sel=signal<PurchaseOrder|null>(null);
  tab=signal<'Summary'|'Item Lines'|'GRN Details'>('Summary');
  grnView=signal<'headers'|'lines'>('headers'); grnSel=signal<AsnHeader|null>(null);
  pos=computed(()=> this.ds.current()?.pos ?? []);
  mat=computed(()=> this.pos().filter(p=>p.cat==='Material'));
  dept=computed(()=> this.pos().filter(p=>p.cat==='Department'));
  rows=computed(()=> this.pos().filter(p=>p.cat===this.sub()));
  matVal=computed(()=> this.mat().reduce((s,p)=>s+p.val,0)/100);
  deptVal=computed(()=> this.dept().reduce((s,p)=>s+p.val,0)/100);
  approved=computed(()=> this.pos().filter(p=>p.appr==='Approved').length);
  lines=computed(()=> this.sel()? (this.ds.current()?.poLines[this.sel()!.no] ?? []) : []);
  grns(ref:string){ return this.ds.current()?.asnHeaders[ref] ?? []; }
  grnLines(no?:string){ return no? (this.ds.current()?.asnLines[no] ?? []) : []; }
  grnCount(ref:string){ return (this.ds.current()?.asnHeaders[ref] ?? []).length; }
  open(p:PurchaseOrder){ this.sel.set(p); this.tab.set('Summary'); this.grnView.set('headers'); }
}
