import { Component, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DataService } from '../../core/services/data.service';
import { KpiTileComponent } from '../../shared/kpi-tile.component';
import { UiCardComponent } from '../../shared/ui-card.component';
import { CrPipe, PctOfPipe } from '../../core/pipes/format.pipes';
import { Router } from '@angular/router';
import { LoaderComponent } from '../../shared/loader.component';

@Component({
  selector: 'cc-overview', 
  standalone: true,
  imports: [CommonModule, KpiTileComponent, UiCardComponent, CrPipe, PctOfPipe],
  template: `


  <div class="page-title">{{ ds.scope()==='entity' ? 'Consolidated ' : '' }}Cost & Contracts <i>Overview</i></div>
  <div class="page-sub" id="pgSub">
          <span>Budget · Commitments · Billing</span><span class="sep">·</span>
          <span>Civil core &amp; shell in progress</span><span class="sep">·</span>
          <span>₹ in Crore unless noted</span>
        </div>
  <ng-container *ngIf="projectCardCount() as x">
    <!-- PROJECT KPIs -->
    <div class="kpi-row" *ngIf="ds.scope()==='project'">
      <cc-kpi cls="navy" label="Sanctioned Budget" [value]="'₹'+(x.budget|cr)" unit="Cr" [sub]="x.counts.wo+' WOs'"></cc-kpi>
      <cc-kpi label="Committed WO/PO" [value]="'₹'+(x.committed|cr)" unit="Cr" [sub]="(x.committed|pctOf:x.budget)+'% of budget'" [bar]="x.committed|pctOf:x.budget"></cc-kpi>
      <cc-kpi cls="good" label="Billed to Date" [value]="'₹'+((x.woBilled+x.directBilled)|cr)" unit="Cr" sub="C + D"></cc-kpi>
      <cc-kpi cls="warn" label="Budget Available" [value]="'₹'+(x.available|cr)" unit="Cr" sub="A − B − E"></cc-kpi>      
    </div>

    <!-- ENTITY KPIs -->
    <div class="kpi-row" *ngIf="ds.scope()==='entity'">
      <cc-kpi 
        cls="navy" 
        label="Portfolio Budget vs Available" 
        [value]="'₹'+(entityData().projects_budget|cr)" 
        unit="Cr" 
        [sub]="entitySubData().budget"
        [bar]="entityPctData().budget">
      </cc-kpi>

      <cc-kpi 
        cls="good" 
        label="WO/PO Committed &amp; Billed" 
        [value]="'₹'+(entityData().committed|cr)" 
        unit="Cr" 
        [sub]="entitySubData().committed"
        [bar]="entityPctData().committed">
      </cc-kpi>

      <cc-kpi 
        label="Vendors engaged" 
        [value]="entityData().vendors" 
        [sub]="'Contractors + material · across portfolio'">
      </cc-kpi>

      <cc-kpi 
        cls="warn" 
        label="Open alerts" 
        [value]="entityData().alerts" 
        [sub]="'Auto-flagged across projects'">
      </cc-kpi>
    </div>

    <!-- PROJECT CARDS -->
    <div class="grid g2" *ngIf="ds.scope()==='project'">
      <div class="card">
        <div class="card-h"><h3>Budget consumption — where the ₹{{ x.budget | cr }} Cr stands</h3><span class="hint">Billed · Committed-not-billed · Available</span></div>
        <div class="card-b">
          <div class="stack">
            <div class="s-billed" [style.flex]="(x.woBilled + x.directBilled) / x.budget">₹{{ (x.woBilled + x.directBilled) | cr }} Cr billed</div>
            <div class="s-commit" [style.flex]="(x.committed - (x.woBilled + x.directBilled)) / x.budget">₹{{ (x.committed - (x.woBilled + x.directBilled)) | cr }} Cr committed</div>
            <div class="s-avail" [style.flex]="x.available / x.budget">₹{{ x.available | cr }} Cr free</div>
          </div>
          <div class="legend">
            <span><i class="dot" style="background:var(--navy)"></i>Billed &amp; posted ({{ ((x.woBilled + x.directBilled) | pctOf:x.budget) }}%)</span>
            <span><i class="dot" style="background:var(--cyan)"></i>Committed, un-billed ({{ ((x.committed - (x.woBilled + x.directBilled)) | pctOf:x.budget) }}%)</span>
            <span><i class="dot" style="background:#E2D8C4"></i>Budget available ({{ (x.available | pctOf:x.budget) }}%)</span>
          </div>
          <div class="note"><b>Reading it:</b> {{ (x.committed | pctOf:x.budget) }}% committed and {{ ((x.woBilled + x.directBilled) | pctOf:x.budget) }}% billed. ₹{{ x.available | cr }} Cr of budget is still available for release; ₹{{ (x.committed - (x.woBilled + x.directBilled)) | cr }} Cr is committed but not yet billed and will settle against future claims.</div>
        </div>
      </div>
      <div class="card">
        <div class="card-h"><h3>Attention</h3><span class="hint">Auto-flagged</span></div>
        <div class="card-b">
          <div class="alert w"><span class="ic">⚠</span><div class="txt"><b>Committed vs billed gap ₹{{ (x.committed - (x.woBilled + x.directBilled)) | cr }} Cr</b> Work committed on WO/PO but not yet certified — track against RA progress.</div></div>
          <div class="alert n"><span class="ic">◷</span><div class="txt"><b>Retention held ₹{{ (x.retentionHeld || 0) | cr }} Cr</b>Withheld across vendors — release schedule tied to defect-liability completion.</div></div>
          <div class="alert b"><span class="ic">●</span><div class="txt"><b>BOQ cost creep</b>Ordered BOQ above design — review the BOQ Comparison tab before further RA certification.</div></div>
          <div class="alert w"><span class="ic">⚠</span><div class="txt"><b>Billed without PO/WO or JV ₹{{ (x.directBilled || 0) | cr }} Cr</b>Bills posted directly to budget codes (not via WO) — verify in Budget & Finance drill.</div></div>
        </div>
      </div>
    </div>
  </ng-container>

  <!-- PROJECTS GRID -->
  <cc-card title="Projects" hint="click a card to open the project cockpit" *ngIf="ds.scope()==='entity'">
    <div class="projects-grid">
      <div *ngFor="let p of ds.projects()" class="proj-card" (click)="open(p.id)">
        <div class="pc-stage">{{ p.stage || 'Active' }}</div>
        <div class="pc-name">
          {{ p.name }}
          <span *ngIf="p" class="samp">SAMPLE</span>
        </div>
        <div class="pc-loc">{{ p.loc || 'Location TBD' }}</div>
        <div class="pc-mini">
          <div>
            <div class="l">Budget</div>
            <div class="v">₹{{ p.budget | cr }}</div>
          </div>
          <div>
            <div class="l">Commited</div>
            <div class="v">₹{{ p.committed | cr }}</div>
          </div>
        </div>
        <div class="pc-mini">
          <div>
            <div class="l">Billed</div>
            <div class="v">₹{{ p.billed | cr }}</div>
          </div>
          <div>
            <div class="l">Available</div>
            <div class="v">₹{{ p.available | cr }}</div>
          </div>
        </div>
        <div class="pc-bar">
          <span [style.width]="(p.committed | pctOf:p.budget) + '%'"></span>
        </div>
        <div class="pc-go">
          {{ p.committed | pctOf:p.budget }}% committed · 
          {{ p.billed | pctOf:p.budget }}% billed · open project →
        </div>
      </div>
    </div>
  </cc-card>

  <div class="note"><b>Structure:</b> this is the <b>consolidated legal-entity</b> view — every KPI rolls up all projects under Joynest Premises Pvt Ltd. Switch to <b>Project</b> (top-left) or click a card to open a single project's detailed cockpit. Projects marked <span class="samp">SAMPLE</span> are placeholders; only Govt. Office (Admin Building) carries mapped data today.</div>
  `,
  styles: [`
    .proj-card{border:1px solid var(--line);border-radius:12px;padding:14px;cursor:pointer;background:#fff}
    .proj-card b{display:block;margin-bottom:8px}
    .kv{display:flex;justify-content:space-between;font-size:12px;color:var(--t2);padding:2px 0}
 
  `]
})
export class OverviewComponent {
  ds = inject(DataService);
  private router = inject(Router);
  projectCardCount = computed(() => this.ds.projectCount()?.projecttotalCount); 
  // ??  {
  //       "budget": 64.41,
  //       "committed": 53.11,
  //       "woBilled": 45.93,
  //       "directBilled": 5.59,
  //       "balance": 7.18,
  //       "available": 5.67,
  //       "retentionHeld": 1.34,
  //       "tdsYtd": 0.42,
  //       "gstInput": 8.43,
  //       "poValue": 1.17,
  //       "grnValue": 1.17,
  //       "counts": {
  //         "asn": 3223,
  //         "inv": 672,
  //         "wo": 40,
  //         "po": 58,
  //         "vendor": 122,
  //         "dept": 45,
  //         "poMat": 13,
  //         "poDept": 45
  //       },
  //       "unapproved": 0,
  //       "unposted": 0,
  //       "poValueMat": 116.61,
  //       "poValueSrv": 1123.94,
  //       "woValueWO": 51.25,
  //       "woValueDept": 11.24,
  //       "poValueDept": 1123.94,
  //       "invPaid": 87.96,
  //       "invBal": 5,
  //       "invTotal": 93.63,
  //       "vendorCount": 122,
  //       "vendorOrderValue": 63.67,
  //       "vendorBilled": 93.62,
  //       "vendorPaid": 87.95,
  //       "vendorBalance": 5,
  //       "vendorRetention": 2.55,
  //       "vendorCats": {
  //         "Contractor": 28,
  //         "Other": 62,
  //         "Service": 25,
  //         "Material": 7
  //       },
  //       "vendorContractors": 28
  //     });


  // Computed entity data for better organization
  entityData = computed(() => {

    const project_counts_and_perc = this.ds.projectCount()?.projecttotalCount;
    const projects = this.ds.projects();
    const budget = this.ds.entitySum('budget');
    const committed = project_counts_and_perc.totalcommitted// this.ds.entitySum('committed');
    const woBilled = this.ds.entitySum('woBilled');
    const directBilled = this.ds.entitySum('directBilled');
    const available = project_counts_and_perc.totalAvailable; //this.ds.entitySum('available');

      // const totalcommitted = project_counts_and_perc.totalcommitted

    //projects budget
    const projects_budget = project_counts_and_perc.totalbudget;//projects.reduce((count, p)=> count + (p.budget || 0), 0) || 0

    // Count vendors across all projects
    const vendors = project_counts_and_perc.totalvendor//projects.reduce((count, p) => count + (p.vendors || 0), 0) || 0;
    
    // Count alerts across all projects
    const alerts = project_counts_and_perc.totalalert;//projects.reduce((count, p) => count + (p.alerts || 0), 0) || 0;

    const UncommittedPer = project_counts_and_perc.UncommittedPer;


    return { budget, committed, woBilled, directBilled, available, vendors, alerts, projects_budget, UncommittedPer };
  });

  

  // Sub text for entity KPIs
  entitySubData = computed(() => {
    const data = this.entityData();
    const project_counts_and_perc = this.ds.projectCount()?.projecttotalCount;
    const budget = data.budget || 0;
    const available = data.available || 0;
    // const committed = data.committed || 0;
    const billed = data.woBilled + data.directBilled || 0;
    const projects_len = this.ds.projects().length;
    const projects = this.ds.projects();

    const pct = (value: number, total: number) => total ? Math.round((value / total) * 100) : 0;
    const projects_budget = projects.reduce((count, p)=> count + (p.budget || 0), 0) || 0
    const proj_available  = projects.reduce((count, a)=> count + (a.available || 0) , 0 )
    const billed_to_date = project_counts_and_perc.totalbilled

  const unCommitedcommitted = project_counts_and_perc.UncommittedPer;
  const commiterdPer = project_counts_and_perc.committedPer

  console.log('commited percentage: ', commiterdPer)


  const cr = (value: number | null | undefined) =>  ((Number(value) || 0) / 1_00_00_000).toFixed(2);
    return {
      budget: `<b style="color:var(--good)">₹${cr(proj_available)} Cr available</b> · ${unCommitedcommitted}% uncommitted · ${projects_len} projects`,
      committed: `committed (${commiterdPer}% of budget) · billed to date <b>₹${cr(billed_to_date)} Cr</b> (${commiterdPer}%)`
    };
  });

  // Percentage values for bar display
  entityPctData = computed(() => {
    const project_counts_and_perc = this.ds.projectCount()?.projecttotalCount;
    const data = this.entityData();    
    // const budget = data.budget || 0;

    const committed = project_counts_and_perc.committedPer // data.committed || 0;
    const billed = data.woBilled + data.directBilled || 0;

    const pct = (value: number, total: number) => total ? Math.round((value / total) * 100) : 0;

    return {
      budget: 100 - project_counts_and_perc.UncommittedPer,//pct(budget - data.available, budget), // % committed
      committed:committed// pct(billed, committed) // % billed of committed
    };
  });

  open(id: string) {
    this.ds.setProject(id);
    this.ds.setScope('project');
    this.router.navigate(['/budget'], { 
      queryParams: { 
        projectId: id
      }
    });
  }
}