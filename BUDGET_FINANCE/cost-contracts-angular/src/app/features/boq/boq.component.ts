import { Component, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DataService } from '../../core/services/data.service';
import { KpiTileComponent } from '../../shared/kpi-tile.component';
import { UiCardComponent } from '../../shared/ui-card.component';
import { CrPipe, InrPipe } from '../../core/pipes/format.pipes';

type ProjectAgg = {
  id: string;
  name: string;
  stage: string;
  sample: boolean;
  boqDesign: number;
  boqOrder: number;
  variance: number;
  varPct: number;
  // billed:number;
};
@Component({
  selector: 'cc-boq', standalone: true,
  imports: [CommonModule, KpiTileComponent, UiCardComponent, CrPipe, InrPipe],
  template: `
  <div class="page-title">BOQ <i>Comparison</i></div>
  <div class="page-sub">Tendered (design) vs ordered — quantity, rate & value variance</div>
  <!-- ================= ENTITY SCOPE ================= -->
  <ng-container *ngIf="ds.scope() === 'entity'">
    <div class="kpi-row">
      <cc-kpi cls="navy" [compact]="true" label="Design BOQ (entity)"
              [value]="'₹' + (totals().design | cr)" unit="Cr" sub="As tendered"></cc-kpi>

      <cc-kpi [compact]="true" label="Ordered BOQ (entity)"
              [value]="'₹' + (totals().order | cr)" unit="Cr"
              [sub]="'+' + totals().varPct + '% vs design'"></cc-kpi>

      <cc-kpi cls="warn" [compact]="true" label="Variance"
              [value]="'₹' + (totals().variance | cr)" unit="Cr" sub="Cost creep"></cc-kpi>

      <cc-kpi cls="good" [compact]="true" label="Projects"
              [value]="projectSummaries().length" sub="Under review"></cc-kpi>
    </div>

    <cc-card title="BOQ variance — by project"
             hint="Design vs ordered · click for line items">

             
      <div class="tbl-scroll">
        <table class="compact">
          <thead>
            <tr>
              <th>Project</th>
              <th class="num">Design BOQ</th>
              <th class="num">Ordered BOQ</th>
              <th class="num">Variance</th>
              <th class="num">Var %</th>
            </tr>
          </thead>
          <tbody>
            <tr class="click" *ngFor="let p of projectSummaries()" (click)="openProject(p.id)">
              <td class="strong">
                {{ p.name }}
                <span class="samp" *ngIf="p.sample">SAMPLE</span>
              </td>
              <td class="num">{{ p.boqDesign | cr }}</td>
              <td class="num">{{ p.boqOrder | cr }}</td>
              <td class="num" [class.neg]="p.variance > 0">
                {{ p.variance > 0 ? '+' : '' }}{{ p.variance | cr }}
              </td>
              <td class="num">
                <span class="stat" [class.st-open]="p.variance > 0" [class.st-paid]="p.variance <= 0">
                  {{ p.variance > 0 ? '+' : '' }}{{ p.varPct }}%
                </span>
              </td>
            </tr>
          </tbody>
          <tfoot>
            <tr class="tfoot">
              <td>Total · ₹ Cr</td>
              <td class="num">{{ totals().design | cr }}</td>
              <td class="num">{{ totals().order | cr }}</td>
              <td class="num">{{ totals().variance | cr }}</td>
              <td class="num">{{ totals().varPct }}%</td>
            </tr>
          </tfoot>
        </table>
      </div>
    </cc-card>
  </ng-container>

  <!-- ================= PROJECT SCOPE ================= -->
  <ng-container *ngIf="ds.scope() === 'project'">
    <div class="kpi-row">
      <cc-kpi [compact]="true" sub="As tendered / SSR" cls="navy" label="Design BOQ" [value]="'₹' + (design() | cr)" unit="Cr"></cc-kpi>
      <cc-kpi [compact]="true"  [sub]="getUtilizedSubText()" label="Ordered BOQ Value" [value]="'₹' + (order() | cr)" unit="Cr"></cc-kpi>
      <cc-kpi [compact]="true"
        [cls]="varPct() > 15 ? 'bad' : 'warn'"
        label="Rate variance items"
        [value]="rateVarianceItems()"
        sub="Above design rate"></cc-kpi>

      <cc-kpi [compact]="true"
        label="Qty within ±2%"
        [value]="qtyWithin2Pct()"
        [sub]="'Of ' + totalLines() + ' shown lines'"></cc-kpi>
    </div>

    <cc-card title="BOQ line comparison" hint="value ₹Cr · rate ₹">
      <table class="compact">
        <thead>
          <tr>
            <th>Code</th><th>Description</th><th>Unit</th>
            <th class="num">Design Qty</th><th class="num">Order Qty</th><th class="num">Qty Δ%</th>
            <th class="num">Design Rate</th><th class="num">Order Rate</th><th class="num">Rate Δ%</th>
            <th class="num">Design ₹</th><th class="num">Order ₹</th><th class="num">Value Δ</th>
          </tr>
        </thead>
        <tbody>
          <tr *ngFor="let b of rows()">
            <td class="mut">{{ b.code }}</td>
            <td class="strong"><span class="trunc w200" [title]="b.desc">{{ b.desc }}</span></td>
            <td>{{ b.u || '—' }}</td>
            <td class="num">{{ b.dq | inr }}</td>
            <td class="num">{{ b.oq | inr }}</td>
            <td class="num" >
              <span class="stat" [class.st-open]="d(b.oq, b.dq) >= 0" [class.st-paid]="d(b.oq, b.dq) < 0"> 
                {{ fmtd(b.oq, b.dq) }}
              </span>           
            </td>
            <td class="num">{{ b.dr | inr }}</td>
            <td class="num">{{ b.or_ | inr }}</td>
            <td class="num" >
              <span class="stat st-open" [class.st-open]="d(b.or_, b.dr) >= 0" [class.st-paid]="d(b.or_, b.dr) <= 0">
                {{ fmtd(b.or_, b.dr) }}
              </span>              
            </td>
            <td class="num">{{ b.bv | cr }}</td>
            <td class="num strong">{{ b.wv | cr }}</td>
            <td class="num" [class.neg]="(b.wv - b.bv) > 0">{{ (b.wv - b.bv) | cr }}</td>
          </tr>
        </tbody>
        <tfoot>
          <tr class="tfoot">
            <td colspan="9">Totals</td>
            <td class="num">{{ design() | cr }}</td>
            <td class="num">{{ order() | cr }}</td>
            <td class="num">{{ gap() | cr }}</td>
          </tr>
        </tfoot>
      </table>
    </cc-card>
  </ng-container>
  `

})
export class BoqComponent {
  ds = inject(DataService);
  rows = computed(() => this.ds.current()?.boq ?? []);
  design = computed(() => this.ds.current()?.boqTot?.design ?? 0);
  order = computed(() => this.ds.current()?.boqTot?.order ?? 0);
  gap = computed(() => this.order() - this.design());
  varPct = computed(() => this.design() ? Math.round(this.gap() / this.design() * 100) : 0);
  varCls = computed(() => this.getPercentageColor(this.varPct()));

  d(a: number, b: number) { return b ? Math.round((a - b) / b * 100) : 0; }
  fmtd(a: number, b: number) { const v = this.d(a, b); return (v > 0 ? '+' : '') + v + '%'; }
  

  portfolioTotal = (key: 'boqDesign' | 'boqOrder') => {
    return this.projectSummaries().reduce((sum, p) => sum + p[key], 0);
  };

  projectSummaries = computed<ProjectAgg[]>(() => {

    return this.ds.projects().map(p => {
      const boqDesign = p.boqDesign ?? 0;
      const boqOrder = p.boqOrder ?? 0;
      const variance = boqOrder - boqDesign;
      return {
        id: p.id,
        name: p.name,
        stage: p.stage || '--',
        sample: !p.isReal,
        boqDesign,
        boqOrder,
        variance,
        varPct: this.d(boqOrder, boqDesign)   // matches pct(v, p.boqDesign)
      };
    });
  });

  /** Entity-level totals — mirrors eSum('boqDesign') / eSum('boqOrder') */
  totals = computed(() => {
    const list = this.projectSummaries();
    const design = list.reduce((s, p) => s + p.boqDesign, 0);
    const order = list.reduce((s, p) => s + p.boqOrder, 0);
    const variance = order - design;
    return {
      design,
      order,
      variance,
      varPct: this.d(order, design)
    };
  });

  // Rate variance items — order rate > design rate
  rateVarianceItems = computed(() =>
    this.rows().filter(b => (b.or_ ?? 0) > (b.dr ?? 0)).length
  );

  // Qty within ±2% — |orderQty - designQty| <= designQty * 2%
  qtyWithin2Pct = computed(() =>
    this.rows().filter(b => {
      const dq = b.dq ?? 0;
      if (!dq) return false;
      return Math.abs((b.oq ?? 0) - dq) <= dq * 0.02;
    }).length
  );

  // Optional: total shown lines, for the "of X" subtext
  totalLines = computed(() => this.rows().length);

  private getPercentageColor(percentage: number): string {
    if (percentage <= 30) return 'r';
    if (percentage <= 60) return 'o';
    if (percentage <= 80) return 'w';
    return '';
  }

  varLabel = computed(() => {
    const p = this.varPct();
    return `${p > 0 ? '+' : ''}${p}% vs design`;
  });

  getUtilizedSubText(): string {
    const pct = this.varPct();                                  // ← number
    const variation = (pct > 0 ? '+' : '') + pct + '% vs design';
    const colorClass = this.getPercentageColor(pct);            // ← pass number
    return `<span class="tag ${colorClass}">${variation}</span>`;
  }

  openProject(projectId: string) {
    this.ds.setProject(projectId);
    if (this.ds.scope() === 'entity') {
      this.ds.setScope('project');
    }
  }



}
