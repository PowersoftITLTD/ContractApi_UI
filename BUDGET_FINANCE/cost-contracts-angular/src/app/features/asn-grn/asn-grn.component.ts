import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DataService } from '../../core/services/data.service';
import { KpiTileComponent } from '../../shared/kpi-tile.component';
import { UiCardComponent } from '../../shared/ui-card.component';
import { StatusSymbolComponent } from '../../shared/status-symbol.component';
import { BackBarComponent } from '../../shared/back-bar.component';
import { InrPipe } from '../../core/pipes/format.pipes';
import { AsnHeader } from '../../core/models/models';
type Flat = AsnHeader & { ref:string };
@Component({
  selector:'cc-asn-grn', standalone:true,
  imports:[CommonModule, KpiTileComponent, UiCardComponent, StatusSymbolComponent, BackBarComponent, InrPipe],
  template:`
  <div class="page-title">ASN / <i>GRN</i></div>
  <div class="page-sub">Goods receipts against WO/PO · header → line</div>
  <div class="kpi-row">
    <cc-kpi cls="navy" label="Receipts (ASN/GRN)" [value]="flat().length"></cc-kpi>
    <cc-kpi cls="good" label="Approved" [value]="approved()" [sub]="'of '+flat().length"></cc-kpi>
    <cc-kpi label="References with receipts" [value]="refs()"></cc-kpi>
    <cc-kpi cls="warn" label="In process" [value]="inproc()"></cc-kpi>
  </div>

  <ng-container *ngIf="view()==='headers'">
    <cc-card title="ASN / GRN headers" hint="click a row for its lines">
      <div class="tbl-scroll">
        <table class="compact">
          <thead><tr><th>ASN/GRN No</th><th>Against WO/PO</th><th>Challan</th><th>Date</th>
            <th class="num">Qty</th><th class="num">Approved</th><th class="num">Billed</th><th>Draft INV</th><th class="ctr">Status</th></tr></thead>
          <tbody>
          <tr class="click" *ngFor="let a of flat()" (click)="open(a)">
            <td><span class="amt-link">{{a.asnNo}}</span></td><td class="mut">{{a.ref}}</td>
            <td class="mut">{{a.challan}}</td><td class="mut">{{a.date}}</td>
            <td class="num">{{a.asnQty|inr}}</td><td class="num">{{a.apprQty|inr}}</td><td class="num">{{a.billQty|inr}}</td>
            <td class="mut">{{a.draftInv||'—'}}</td><td class="ctr"><cc-sym kind="asn" [value]="a.status"></cc-sym></td>
          </tr>
        </tbody>
      </table>
      </div>     
    </cc-card>
  </ng-container>

  <ng-container *ngIf="view()==='lines'">
    <cc-backbar label="ASN / GRN list" (back)="view.set('headers')"></cc-backbar>
    <div class="drill-head">{{sel()?.asnNo}} · {{sel()?.challan}}</div>
    <div class="drill-sub">against {{sel()?.ref}} · {{sel()?.date}} · {{sel()?.status}}</div>
    <cc-card>
      <div class="tbl-scroll">
      <table class="compact">
        <thead><tr><th>Document No</th><th>Draft INV</th><th>Vendor Bill</th>
          <th class="num">Qty</th><th class="num">Approved</th><th class="num">Billed</th><th>Responsible</th></tr></thead>
        <tbody><tr *ngFor="let l of lines()">
          <td class="mut">{{l.docNo}}</td><td class="mut">{{l.draftInv||'—'}}</td><td class="mut">{{l.vendBill||'—'}}</td>
          <td class="num">{{l.asnQty|inr}}</td><td class="num">{{l.apprQty|inr}}</td><td class="num">{{l.billQty|inr}}</td>
          <td class="mut">{{l.responsible||'—'}}</td></tr></tbody>
      </table>
      </div>
    </cc-card>
  </ng-container>`
})
export class AsnGrnComponent {
  ds=inject(DataService);
  view=signal<'headers'|'lines'>('headers'); sel=signal<Flat|null>(null);
  flat=computed<Flat[]>(()=>{
    const h=this.ds.current()?.asnHeaders ?? {}; const out:Flat[]=[];
    for(const ref of Object.keys(h)) for(const a of h[ref]) out.push({...a, ref});
    return out.sort((x,y)=> (y.date||'').localeCompare(x.date||''));
  });
  approved=computed(()=> this.flat().filter(a=>a.status==='Approved').length);
  inproc=computed(()=> this.flat().filter(a=>/Process/.test(a.status)).length);
  refs=computed(()=> new Set(this.flat().map(a=>a.ref)).size);
  lines=computed(()=> this.sel()? (this.ds.current()?.asnLines[this.sel()!.asnNo] ?? []) : []);
  open(a:Flat){ this.sel.set(a); this.view.set('lines'); }
}
