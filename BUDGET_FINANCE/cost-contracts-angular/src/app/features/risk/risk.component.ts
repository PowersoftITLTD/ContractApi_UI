import { Component, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DataService } from '../../core/services/data.service';
import { KpiTileComponent } from '../../shared/kpi-tile.component';
import { UiCardComponent } from '../../shared/ui-card.component';
import { CrPipe } from '../../core/pipes/format.pipes';
import { BudgetRow } from '../../core/models/models';
type Risk = { sev:'high'|'med'|'low'; cat:string; title:string; metric:string; desc:string; action:string; exp:number };
type CodeRisk = BudgetRow & { consumed:number; uc:number; flag:string; sev:'high'|'med' };
@Component({
  selector:'cc-risk', standalone:true,
  imports:[CommonModule, KpiTileComponent, UiCardComponent, CrPipe],
  template:`
  <div class="page-title">Risk <i>Factors</i></div>
  <div class="page-sub">Auto-derived cost, contract & billing risks · severity-ranked</div>
  <div class="kpi-row">
    <cc-kpi [cls]="rating().cls" label="Overall Risk" [value]="rating().label" [sub]="high()+' high · '+risks().length+' factors'"></cc-kpi>
    <cc-kpi cls="navy" label="Exposure at Risk" [value]="'₹'+(exposure()|cr)" unit="Cr" [sub]="pct(exposure(),B())+'% of budget'"></cc-kpi>
    <cc-kpi [cls]="util()>=95?'bad':util()>=85?'warn':'good'" label="Committed Utilisation" [value]="util()+'%'" [sub]="'billed '+pct(billed(),B())+'%'"></cc-kpi>
    <cc-kpi cls="warn" label="Off-control + Uncertified" [value]="'₹'+((offD()+E())|cr)" unit="Cr" sub="No-PO/WO (D) + unbilled (E)"></cc-kpi>
  </div>

  <cc-card title="Budget codes at risk" [hint]="atRisk().length+' codes · ranked by commitment'" *ngIf="atRisk().length">
    <table class="compact">
      <thead><tr><th>Code</th><th>Description</th><th class="num">Budget (A)</th><th class="num">Committed (B)</th>
        <th class="num">Billed (C+D)</th><th class="num">Committed %</th><th class="num">Available (F)</th><th class="ctr">Flag</th></tr></thead>
      <tbody>
        <tr *ngFor="let c of atRisk()">
          <td class="mut">{{c.code}}</td><td class="strong"><span class="trunc w200" [title]="c.desc">{{c.desc}}</span></td>
          <td class="num">{{c.A|cr}}</td><td class="num">{{c.B|cr}}</td><td class="num">{{c.consumed|cr}}</td>
          <td class="num strong" [class.neg]="c.uc>=100">{{c.uc}}%</td>
          <td class="num" [class.neg]="c.avail<0">{{c.avail|cr}}</td>
          <td class="ctr"><span class="stat" [class.st-hold]="c.sev==='high'" [class.st-open]="c.sev==='med'">{{c.flag}}</span></td>
        </tr>
      </tbody>
    </table>
  </cc-card>

  <cc-card title="Risk register" hint="severity scaled to budget size">
    <div class="risk-grid">
      <div class="risk-card {{r.sev}}" *ngFor="let r of risks()">
        <div class="risk-top"><div><div class="risk-cat">{{r.cat}}</div><div class="risk-title">{{r.title}}</div></div>
          <span class="risk-sev {{r.sev}}">{{r.sev==='med'?'Medium':r.sev}}</span></div>
        <div class="risk-metric">{{r.metric}}</div><div class="risk-desc">{{r.desc}}</div>
        <div class="risk-action">→ {{r.action}}</div>
      </div>
    </div>
  </cc-card>`
})
export class RiskComponent {
  ds=inject(DataService);
  d=computed(()=> this.ds.current());
  B=computed(()=> this.d()?.totals.budget ?? 0);
  C=computed(()=> this.d()?.totals.committed ?? 0);
  billed=computed(()=> (this.d()?.totals.woBilled ?? 0)+(this.d()?.totals.directBilled ?? 0));
  E=computed(()=> this.d()?.totals.balance ?? 0);
  offD=computed(()=> this.d()?.totals.directBilled ?? 0);
  util=computed(()=> this.pct(this.C(), this.B()));
  pct(a:number,b:number){ return b? Math.round(a/b*100):0; }

  atRisk=computed<CodeRisk[]>(()=>{
    const rows=(this.d()?.budget ?? []).filter(b=>b.A>0).map(b=>{
      const consumed=b.C+b.D, uc=this.pct(b.B,b.A); let flag='',sev:'high'|'med'|''='';
      if(b.avail<0){flag='Over budget';sev='high';}
      else if(uc>=100){flag='Fully committed';sev='high';}
      else if(uc>=90){flag='Near budget';sev='med';}
      else if(this.pct(consumed,b.A)>=85){flag='High billing';sev='med';}
      return {...b, consumed, uc, flag, sev} as CodeRisk & {sev:any};
    }).filter(c=>c.sev);
    return rows.sort((a,b)=> ((a.avail<0?0:1)-(b.avail<0?0:1)) || b.uc-a.uc);
  });
  breach=computed(()=> this.atRisk().filter(c=>c.avail<0).reduce((s,c)=>s+Math.abs(c.avail),0));
  boqGap=computed(()=>{ const t=this.d()?.boqTot; return t? t.order-t.design:0; });
  exposure=computed(()=> this.boqGap()+this.E()+this.offD()+this.breach());
  high=computed(()=> this.risks().filter(r=>r.sev==='high').length);
  rating=computed(()=>{ const h=this.high(), r=this.B()? this.exposure()/this.B():0;
    if(h>=2||r>.4) return {label:'High',cls:'bad'}; if(h>=1||r>.2) return {label:'Elevated',cls:'warn'};
    if(r>.08) return {label:'Moderate',cls:'warn'}; return {label:'Low',cls:'good'}; });

  risks=computed<Risk[]>(()=>{
    const d=this.d(); if(!d) return []; const t=d.totals; const out:Risk[]=[];
    const add=(sev:any,cat:string,title:string,metric:string,desc:string,action:string,exp=0)=>out.push({sev,cat,title,metric,desc,action,exp});
    if(this.breach()>0){ const over=this.atRisk().filter(c=>c.avail<0);
      add('high','Budget','Budget over-runs',`${over.length} code(s) · ₹${this.cr(this.breach())} Cr over`,
        `${over.slice(0,4).map(c=>c.code).join(', ')} committed/billed beyond sanctioned budget.`,'Budget revision before further orders.',this.breach()); }
    if(d.boqTot){ const v=this.pct(this.boqGap(),d.boqTot.design);
      add(v>40?'high':v>20?'med':'low','BOQ',`BOQ escalation +${v}% vs design`,`₹${this.cr(this.boqGap())} Cr over design`,
        `Ordered ₹${this.cr(d.boqTot.order)} Cr vs design ₹${this.cr(d.boqTot.design)} Cr.`,'Challenge rate/qty variances.',this.boqGap()); }
    if(this.E()>0){ const r=this.pct(this.E(),this.B());
      add(r>20?'high':r>10?'med':'low','Commitment','Uncertified commitment (E)',`₹${this.cr(this.E())} Cr · ${r}% of budget`,
        'WO/PO issued but not yet billed/certified.','Track RA certification pace.',this.E()); }
    if(this.offD()>0){ const r=this.pct(this.offD(),this.billed());
      add(r>15?'high':r>8?'med':'low','Governance','Billing without PO/WO or JV (D)',`₹${this.cr(this.offD())} Cr · ${r}% of billed`,
        'Spend booked bypassing WO/PO 3-way control.','Route recurring spend through WO/PO.',this.offD()); }
    const vp=d.vendorPerf; if(vp?.length && t.vendorOrderValue){ const tp=vp[0]; const sh=this.pct(tp.orderValue,t.vendorOrderValue);
      if(sh>25) add(sh>40?'high':'med','Vendor','Vendor concentration',`${tp.name.split(' ').slice(0,2).join(' ')} · ${sh}%`,
        `Top vendor holds ₹${this.cr(tp.orderValue)} Cr of ₹${this.cr(t.vendorOrderValue)} Cr.`,'Diversify / guarantees.'); }
    if((t.invBal||0)>0){ const r=this.pct(t.invBal!,t.invTotal||1);
      add(r>10?'med':'low','Payables','Open / unpaid invoices',`₹${this.cr(t.invBal!)} Cr · ${r}% unpaid`,
        `₹${this.cr(t.invBal!)} Cr unpaid of ₹${this.cr(t.invTotal!)} Cr billed.`,'Prioritise ageing payables.',t.invBal!); }
    const ord:any={high:0,med:1,low:2};
    return out.sort((a,b)=> ord[a.sev]-ord[b.sev] || b.exp-a.exp);
  });
  cr(n:number){ return (Number(n)||0).toLocaleString('en-IN',{maximumFractionDigits:2}); }
}
