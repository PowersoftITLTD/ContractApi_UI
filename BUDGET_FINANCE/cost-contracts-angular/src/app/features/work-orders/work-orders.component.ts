import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DataService } from '../../core/services/data.service';
import { KpiTileComponent } from '../../shared/kpi-tile.component';
import { UiCardComponent } from '../../shared/ui-card.component';
import { StatusSymbolComponent } from '../../shared/status-symbol.component';
import { DrillDrawerComponent } from '../../shared/drill-drawer.component';
import { BackBarComponent } from '../../shared/back-bar.component';
import { CrPipe, InrPipe } from '../../core/pipes/format.pipes';
import { WorkOrder, AsnHeader } from '../../core/models/models';

@Component({
  selector:'cc-work-orders', standalone:true,
  imports:[CommonModule, KpiTileComponent, UiCardComponent, StatusSymbolComponent,
           DrillDrawerComponent, BackBarComponent, CrPipe, InrPipe],
  template:`
  <div class="page-title">Work <i>Orders</i> — Contractors</div>
  <div class="page-sub">Labour & civil commitments · RA bills, ASN, approvals & retention</div>

  <div class="kpi-row">
    <cc-kpi cls="navy" label="Work Orders" [value]="wos().length" sub="Type = Work Order · civil"></cc-kpi>
    <cc-kpi label="WO value" [value]="'₹'+(val()|cr)" unit="Cr" sub="Contractor commitments"></cc-kpi>
    <cc-kpi cls="violet" label="Retention held" [value]="'₹'+(retHeld()|cr)" unit="Cr" sub="5% on eligible WOs"></cc-kpi>
    <cc-kpi cls="good" label="Approved" [value]="approved()" [sub]="'of '+wos().length"></cc-kpi>
  </div>

  

  <cc-card title="Work orders — contractor commitments" hint="₹ Cr · GL Date desc · click WO No for lines">
    <table class="compact">
      <thead><tr>
        <th>WO No</th><th>GL<span class="cc">Date</span></th><th>Department</th>
        <th class="num">Amount<span class="cc">(₹Cr)</span></th><th class="num">Ret<span class="cc">%</span></th>
        <th class="num">WCT<span class="cc">Ret %</span></th><th>Created<span class="cc">By</span></th>
        <th class="ctr">Appr</th><th class="ctr">Bill</th><th class="ctr"># ASN</th>
        <th>Last ASN<span class="cc">No</span></th><th>ASN<span class="cc">Date</span></th><th class="ctr">ASN<span class="cc">Status</span></th>
      </tr></thead>
      <tbody>
        <tr class="click" *ngFor="let w of wos()" (click)="open(w)">
          <td><span class="amt-link">{{w.no}}</span></td>
          <td class="mut">{{w.glDate||'—'}}</td>
          <td><span class="trunc w120" [title]="w.dept">{{w.dept||'—'}}</span></td>
          <td class="num strong">₹{{w.amount|cr}}</td>
          <td class="num">{{w.ret||0}}%</td>
          <td class="num">{{ w.wctRet==null ? '—' : w.wctRet+'%' }}</td>
          <td class="mut"><span class="trunc w120" [title]="w.createdBy">{{w.createdBy||'—'}}</span></td>
          <td class="ctr"><cc-sym kind="approval" [value]="w.appr"></cc-sym></td>
          <td class="ctr"><cc-sym kind="billing" [value]="w.billStatus"></cc-sym></td>
          <td class="ctr">{{ asnCount(w.no) || '—' }}</td>
          <td class="mut">{{w.asnNo||'—'}}</td>
          <td class="mut">{{w.asnDate||'—'}}</td>
          <td class="ctr"><cc-sym *ngIf="w.asnNo" kind="asn" [value]="w.asnStatus"></cc-sym><span *ngIf="!w.asnNo">—</span></td>
        </tr>
      </tbody>
    </table>
  </cc-card>

  <!-- DRILL DRAWER -->
  <cc-drawer [open]="!!sel()" eyebrow="Work Order" [title]="sel()?.no || ''"
             [subtitle]="(sel()?.vendor||'') + ' · Work Order'"
             [tabs]="['Summary','Item Lines','ASN Details']" [active]="tab()"
             (pick)="tab.set($event); asnView.set('headers')" (close)="close()">

    <ng-container *ngIf="sel() as w">
      <!-- Summary -->
      <ng-container *ngIf="tab()==='Summary'">
        <div class="dr-kpis">
          <div class="dr-k"><div class="l">Amount</div><div class="v">₹{{w.amount|cr}} Cr</div></div>
          <div class="dr-k"><div class="l">Retention</div><div class="v">{{w.ret||0}}%</div></div>
          <div class="dr-k"><div class="l">Lines</div><div class="v">{{lines().length}}</div></div>
        </div>
        <table class="kv-tbl">
          <tr><td>Vendor</td><td>{{w.vendor}}</td></tr>
          <tr><td>GL Date</td><td>{{w.glDate||'—'}}</td></tr>
          <tr><td>Department</td><td>{{w.dept||'—'}}</td></tr>
          <tr><td>Approval</td><td>{{w.appr}}</td></tr>
          <tr><td>Billing</td><td>{{w.billStatus}}</td></tr>
          <tr><td>Last ASN</td><td>{{w.asnNo||'—'}}</td></tr>
        </table>
      </ng-container>

      <!-- Item Lines -->
      <ng-container *ngIf="tab()==='Item Lines'">
        <table class="compact" *ngIf="lines().length; else noLines">
          <thead><tr><th>Item</th><th>Description</th><th class="num">Qty</th><th class="num">Rate</th>
            <th class="num">Amount</th><th class="num">Tax</th><th class="num">Billed Qty</th><th class="num">WP Ret %</th><th>Code</th></tr></thead>
          <tbody><tr *ngFor="let l of lines()">
            <td class="mut">{{l.item}}</td><td><span class="trunc w220" [title]="l.desc">{{l.desc}}</span></td>
            <td class="num">{{l.qty|inr}}</td><td class="num">{{l.rate|inr}}</td><td class="num strong">{{l.amt|inr}}</td>
            <td class="num">{{l.tax|inr}}</td><td class="num">{{l.bqty|inr}}</td>
            <td class="num">{{ l.wpRet==null ? '—' : l.wpRet+'%' }}</td>
            <td class="mut">{{l.code}}<div class="sub" *ngIf="l.cdesc">{{l.cdesc}}</div></td>
          </tr></tbody>
        </table>
        <ng-template #noLines><div class="note">No item lines found for this WO.</div></ng-template>
      </ng-container>

      <!-- ASN Details (two-level) -->
      <ng-container *ngIf="tab()==='ASN Details'">
        <ng-container *ngIf="asnHeaders(w.no).length; else noAsn">
          <!-- headers -->
          <ng-container *ngIf="asnView()==='headers'">
            <div class="sec-lbl">{{asnHeaders(w.no).length}} ASN(s) · click an ASN No for its lines</div>
            <table class="compact">
              <thead><tr><th>ASN No</th><th>Challan</th><th>Date</th><th class="num">ASN Qty</th>
                <th class="num">Approved</th><th class="num">Billed</th><th>Draft INV</th><th class="ctr">Status</th></tr></thead>
              <tbody><tr class="click" *ngFor="let a of asnHeaders(w.no)" (click)="openAsn(a)">
                <td><span class="amt-link">{{a.asnNo}}</span></td><td class="mut">{{a.challan}}</td><td class="mut">{{a.date}}</td>
                <td class="num">{{a.asnQty|inr}}</td><td class="num">{{a.apprQty|inr}}</td><td class="num">{{a.billQty|inr}}</td>
                <td class="mut">{{a.draftInv||'—'}}</td><td class="ctr"><cc-sym kind="asn" [value]="a.status"></cc-sym></td>
              </tr></tbody>
            </table>
          </ng-container>
          <!-- lines -->
          <ng-container *ngIf="asnView()==='lines'">
            <cc-backbar label="ASN list" (back)="asnView.set('headers')"></cc-backbar>
            <div class="drill-head">{{asnSel()?.asnNo}} · {{asnSel()?.challan}}</div>
            <div class="drill-sub">{{asnSel()?.date}} · {{asnSel()?.status}}</div>
            <table class="compact">
              <thead><tr><th>Document No</th><th>Draft INV</th><th>Vendor Bill</th>
                <th class="num">ASN Qty</th><th class="num">Approved</th><th class="num">Billed</th><th>Responsible</th></tr></thead>
              <tbody><tr *ngFor="let l of asnLines(asnSel()?.asnNo)">
                <td class="mut">{{l.docNo}}</td><td class="mut">{{l.draftInv||'—'}}</td><td class="mut">{{l.vendBill||'—'}}</td>
                <td class="num">{{l.asnQty|inr}}</td><td class="num">{{l.apprQty|inr}}</td><td class="num">{{l.billQty|inr}}</td>
                <td class="mut">{{l.responsible||'—'}}</td>
              </tr></tbody>
            </table>
          </ng-container>
        </ng-container>
        <ng-template #noAsn><div class="note">No ASN raised against this order yet.</div></ng-template>
      </ng-container>
    </ng-container>
  </cc-drawer>`,
  styles:[`.dr-kpis{display:flex;gap:10px;margin-bottom:12px}
  .dr-k{flex:1;background:var(--bg);border:1px solid var(--line);border-radius:10px;padding:9px 11px}
  .dr-k .l{font-size:10px;color:var(--t3);text-transform:uppercase;letter-spacing:.4px}
  .dr-k .v{font-family:var(--serif);font-size:19px}
  .kv-tbl{width:100%;font-size:12.5px}.kv-tbl td{padding:6px 4px;border-bottom:1px solid var(--line)}
  .kv-tbl td:first-child{color:var(--t2);width:38%}`]
})
export class WorkOrdersComponent {
  ds=inject(DataService);
  sel=signal<WorkOrder|null>(null);
  tab=signal<string>('Summary');
  asnView=signal<'headers'|'lines'>('headers');
  asnSel=signal<AsnHeader|null>(null);

  wos=computed(()=> this.ds.current()?.wos ?? []);
  val=computed(()=> this.wos().reduce((s,w)=>s+w.amount,0));
  retHeld=computed(()=> this.wos().reduce((s,w)=>s+w.amount*(w.ret||0)/100,0));
  approved=computed(()=> this.wos().filter(w=>w.appr==='Approved').length);

  lines=computed(()=> this.sel()? (this.ds.current()?.woLines[this.sel()!.no] ?? []) : []);
  asnHeaders(ref:string){ return this.ds.current()?.asnHeaders[ref] ?? []; }
  asnLines(asnNo?:string){ return asnNo ? (this.ds.current()?.asnLines[asnNo] ?? []) : []; }
  asnCount(ref:string){ return (this.ds.current()?.asnHeaders[ref] ?? []).length; }

  open(w:WorkOrder){ this.sel.set(w); this.tab.set('Summary'); this.asnView.set('headers'); }
  openAsn(a:AsnHeader){ this.asnSel.set(a); this.asnView.set('lines'); }
  close(){ this.sel.set(null); }
}
