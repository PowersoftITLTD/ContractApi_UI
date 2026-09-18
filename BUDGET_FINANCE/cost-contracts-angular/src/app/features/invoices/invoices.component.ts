import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DataService } from '../../core/services/data.service';
import { KpiTileComponent } from '../../shared/kpi-tile.component';
import { UiCardComponent } from '../../shared/ui-card.component';
import { StatusSymbolComponent } from '../../shared/status-symbol.component';
import { DrillDrawerComponent } from '../../shared/drill-drawer.component';
import { CrPipe, InrPipe } from '../../core/pipes/format.pipes';
import { Invoice } from '../../core/models/models';
@Component({
  selector:'cc-invoices', standalone:true,
  imports:[CommonModule, KpiTileComponent, UiCardComponent, StatusSymbolComponent, DrillDrawerComponent, CrPipe, InrPipe],
  template:`
  <div class="page-title">Invoices & <i>RA</i></div>
  <div class="page-sub">Vendor bills & running-account certification · ₹ · GL Date desc</div>
  <div class="kpi-row" *ngIf="ds.scope() === 'entity'">
    <cc-kpi cls="navy" label="Bills" [value]="rows().length"></cc-kpi>
    <cc-kpi label="Billed" [value]="'₹'+(billed()|cr)" unit="Cr"></cc-kpi>
    <cc-kpi cls="good" label="Paid" [value]="'₹'+(paid()|cr)" unit="Cr" [sub]="(paid()|number:'1.0-0')"></cc-kpi>
    <cc-kpi cls="warn" label="Open balance" [value]="'₹'+(bal()|cr)" unit="Cr"></cc-kpi>
  </div>
  <cc-card title="Vendor invoices" hint="₹ · click Invoice No for details">
    <table class="compact">
      <thead><tr><th>Invoice No</th><th>Vendor</th><th>GL<span class="cc">Date</span></th><th>Department</th>
        <th class="ctr">Appr</th><th class="ctr">Pay</th><th class="num">Bill Amount</th><th class="num">Paid Amt</th><th class="num">Balance Amt</th></tr></thead>
      <tbody>
        <tr class="click" *ngFor="let v of rows()" (click)="open(v)">
          <td><span class="amt-link">{{v.no}}</span></td>
          <td><span class="trunc w160" [title]="v.vendor">{{v.vendor}}</span></td>
          <td class="mut">{{v.glDate}}</td><td><span class="trunc w120" [title]="v.dept">{{v.dept}}</span></td>
          <td class="ctr"><cc-sym kind="approval" [value]="v.appr"></cc-sym></td>
          <td class="ctr"><cc-sym kind="payment" [value]="v.payStatus"></cc-sym></td>
          <td class="num strong">{{v.amt|inr}}</td><td class="num">{{v.paid|inr}}</td>
          <td class="num" [class.neg]="v.balance>0">{{v.balance|inr}}</td>
        </tr>
      </tbody>
    </table>
  </cc-card>

  <cc-drawer [open]="!!sel()" eyebrow="Invoice" [title]="sel()?.no||''"
             [subtitle]="(sel()?.vendor||'')+' · '+(sel()?.type||'')"
             [tabs]="['Summary','Invoice Item Details']" [active]="tab()" (pick)="tab.set($any($event))" (close)="sel.set(null)" *ngIf="ds.scope() === 'project'">
    <ng-container *ngIf="sel() as v">
      <ng-container *ngIf="tab()==='Summary'">
        <table class="kv-tbl">
          <tr><td>WO / PO</td><td>{{v.wopo||'— (billed without PO/WO)'}}</td></tr>
          <tr><td>Vendor</td><td>{{v.vendor}}</td></tr>
          <tr><td>GL Date</td><td>{{v.glDate}}</td></tr>
          <tr><td>Department</td><td>{{v.dept}}</td></tr>
          <tr><td>Type</td><td>{{v.type}}</td></tr>
          <tr><td>Approval</td><td>{{v.appr}}</td></tr>
          <tr><td>Payment</td><td>{{v.payStatus}}</td></tr>
          <tr><td>Retention</td><td>{{v.ret||0}}%</td></tr>
          <tr><td>Bill / Paid / Balance</td><td>₹{{v.amt|inr}} / ₹{{v.paid|inr}} / ₹{{v.balance|inr}}</td></tr>
          <tr><td>Memo</td><td>{{v.memo||'—'}}</td></tr>
        </table>
      </ng-container>
      <ng-container *ngIf="tab()==='Invoice Item Details'">
        <table class="compact" *ngIf="lines().length; else nl">
          <thead><tr><th>Item</th><th>Description</th><th class="num">Qty</th><th class="num">Rate</th>
            <th class="num">Amount</th><th class="num">Tax</th><th>Code</th></tr></thead>
          <tbody><tr *ngFor="let l of lines()">
            <td class="mut">{{l.item}}</td><td><span class="trunc w220" [title]="l.desc">{{l.desc}}</span></td>
            <td class="num">{{l.qty|inr}}</td><td class="num">{{l.rate|inr}}</td><td class="num strong">{{l.amt|inr}}</td>
            <td class="num">{{l.tax|inr}}</td><td class="mut">{{l.code}}<div class="sub">{{l.cdesc}}</div></td></tr></tbody>
        </table><ng-template #nl><div class="note">No line details.</div></ng-template>
      </ng-container>
    </ng-container>
  </cc-drawer>`,
  styles:[`.kv-tbl{width:100%;font-size:12.5px}.kv-tbl td{padding:6px 4px;border-bottom:1px solid var(--line)}.kv-tbl td:first-child{color:var(--t2);width:38%}`]
})
export class InvoicesComponent {
  ds=inject(DataService);
  sel=signal<Invoice|null>(null); tab=signal<'Summary'|'Invoice Item Details'>('Summary');
  rows=computed(()=> this.ds.current()?.invoices ?? []);
  billed=computed(()=> this.rows().reduce((s,v)=>s+v.amt,0)/1e7);
  paid=computed(()=> this.rows().reduce((s,v)=>s+v.paid,0)/1e7);
  bal=computed(()=> this.rows().reduce((s,v)=>s+v.balance,0)/1e7);
  lines=computed(()=> this.sel()? (this.ds.current()?.invLines[this.sel()!.no] ?? []) : []);
  open(v:Invoice){ this.sel.set(v); this.tab.set('Summary'); }
}
