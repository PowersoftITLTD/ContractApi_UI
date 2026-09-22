import { Component, computed, effect, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DataService } from '../../core/services/data.service';
import { KpiTileComponent } from '../../shared/kpi-tile.component';
import { UiCardComponent } from '../../shared/ui-card.component';
import { BackBarComponent } from '../../shared/back-bar.component';
import { CrPipe, RupPipe, PctOfPipe } from '../../core/pipes/format.pipes';
import { BilledDetail, BudgetRow } from '../../core/models/models';

type GroupAgg = { grp: string; desc: string; budget: number; util: number; bal: number; rate: number | null };
type ProjectAgg = {
  id: string;
  name: string;
  stage: string;
  sample: boolean;
  budget: number;
  committed: number;
  billed: number;
  available: number;
  pct: number;
};

interface WopoDetail {
  no: string;
  date: string;
  status: string;
  vendor: string;
  item: string;
  desc: string;
  unit: string;
  qty: number;
  rate: number;
  amt: number;
  bqty: number;
  bamt: number;
  code: string;
}

@Component({
  selector: 'cc-budget',
  standalone: true,
  imports: [CommonModule, KpiTileComponent, UiCardComponent, BackBarComponent, CrPipe, RupPipe],
  template: `
    <!-- Conditional header based on scope -->
    <div class="crumb" id="crumb">{{ds.entity()?.name}} › {{ds.entity()?.group}} › <b>Project Budget &amp; Finance</b></div>
    <div class="page-title" *ngIf="ds.scope() === 'entity' && ds.level()==='summary'">
      Consolidated Budget & <i>Finance</i>
    </div>

    <div class="page-title" *ngIf="ds.scope() === 'project'">Budget & <i>Finance</i></div>
    <div class="page-sub">Sanctioned vs committed vs billed · click a row to drill</div>

    <!-- CONSOLIDATED VIEW (entity scope) -->
    <ng-container *ngIf="ds.scope() === 'entity' && ds.level()==='summary'">
      <div class="kpi-row">
        <cc-kpi cls="navy" [compact]="true" label="Portfolio Budget" [value]="'₹'+(portfolioTotal('budget')|cr)" unit="Cr" sub="A — all projects"></cc-kpi>
        <cc-kpi label="Committed" [compact]="true" [value]="'₹'+(portfolioTotal('committed')|cr)" unit="Cr" sub="B — WO/PO issued"></cc-kpi>
        <cc-kpi cls="good" [compact]="true" label="Billed Against commited" [value]="'₹'+(portfolioTotal('billed')|cr)" unit="Cr" sub="C - Billed Against WO/PO"></cc-kpi>
        <cc-kpi cls="good" [compact]="true" label="Direct Expense" [value]="'--'"  sub="D - Billed Without WO/PO & JV"></cc-kpi> <!--'₹'+(portfolioTotal('billed')|cr) unit="Cr"-->
        <cc-kpi cls="warn" [compact]="true" label="Available" [value]="'₹'+(portfolioTotal('available')|cr)" unit="Cr" sub="=A − B − D"></cc-kpi>
      </div>

      <cc-card title="Budget Finance — by project" hint="Consolidated across the legal entity · click a project for budget-code detail">
        <div class=tbl-scroll>
       <table class="compact">
  <thead>
    <tr>
      <th rowspan="2">Project</th>
      <th colspan="2" class="num">Construction in (sqft)</th>
      <th colspan="2" class="num">Carpet in (sqft)</th>
      <th rowspan="2" class="num">Budget (A)</th>
      <th rowspan="2" class="num">Committed (B)</th>
      <th rowspan="2" class="num">Direct Expense<br>(D-Billed Without WO/PO & JV)</th>
      <th rowspan="2" class="num">Available<br>(A − B − D)</th>
      <th rowspan="2">Commitment</th>
    </tr>
    <tr>
      <th class="num">Area</th>
      <th class="num">₹ Rate</th>
      <th class="num">Area</th>
      <th class="num">₹ Rate</th>
    </tr>
  </thead>
  <tbody>
    <tr class="click" *ngFor="let p of projectSummaries()" (click)="openProject(p.id)">
      <td class="strong">
        {{p.name}}
        <span class="samp" *ngIf="p.sample">Residence</span>
        <div class="mut" style="font-weight:400;font-size:10.5px">{{p.stage}}</div>
      </td>
      <td class="num strong">--</td>
      <td class="num strong">--</td>
      <td class="num strong">--</td>
      <td class="num strong">--</td>
      <td class="num strong">{{ p.budget | cr }}</td>
      <td class="num">{{ p.committed | cr }}</td>
      <td class="num">{{ p.billed | cr }}</td>
      <td class="num" [class.neg]="p.available < 0">{{ p.available | cr }}</td>
      <td style="width:130px">
        <div class="mini-bar">
          <span [style.width.%]="p.pct"></span>
        </div>
        <div class="mut" style="font-size:10px;margin-top:3px">{{p.pct}}% committed</div>
      </td>
    </tr>
  </tbody>
  <tfoot>
    <tr class="tfoot">
      <td>Total · ₹ Cr</td>
      <td></td>
      <td></td>
      <td></td>
      <td></td>
      <td class="num">{{ portfolioTotal('budget') | cr }}</td>
      <td class="num">{{ portfolioTotal('committed') | cr }}</td>
      <td class="num">{{ portfolioTotal('billed') | cr }}</td>
      <td class="num">{{ portfolioTotal('available') | cr }}</td>
      <td></td>
    </tr>
  </tfoot>
</table>
        </div>
      </cc-card>
    </ng-container>

    <!-- PROJECT VIEW (project scope) -->
    <ng-container *ngIf="ds.scope() === 'project' && ds.level()==='summary'">
      <div class="kpi-row">
        <cc-kpi [compact]="true" cls="navy" label="Project Budget" [value]="'₹'+(tB()|cr)" unit="Cr" [sub]="'--'"></cc-kpi>
        <cc-kpi [compact]="true" cls="navy" label="Construction Area Rate" [value]="'₹'+ 'XXX'" unit="Cr" [sub]="'Overall per sqft'"></cc-kpi>
        <cc-kpi [compact]="true" cls="navy" label="Carpet Area Rate" [value]="'₹'+ 'XXX'" unit="Cr" [sub]="'Carpt. rate inc of non tower area'"></cc-kpi>
        <cc-kpi
          cls="good"
          [compact]="true"
          label="Utilized"
          [value]="'₹'+(tU()|cr)"
          unit="Cr"
          [sub]="getUtilizedSubText()">
        </cc-kpi>        
        <cc-kpi [compact]="true" cls="warn" label="Balance" [value]="'₹'+(tBal()|cr)" unit="Cr" sub="Budget − Utilized"></cc-kpi>
        <cc-kpi [compact]="true" label="Overall Rate / SqFt" [value]="area() ? ('₹'+round(oRate())) : '—'" [sub]="area()? (area()|number)+' SqFt' : 'area pending'"></cc-kpi>
      </div>
      
      <cc-card title="Project Budget Approval — Budget Summary" hint="click a group to open its 9-series codes">
        <div class="tbl-scroll">
          <table class="compact">
            <thead>
              <tr>
                <th rowspan="2">Budget Group</th>
                <!-- <th class="num">Construction Area Rate</th> -->
                <th colspan="2" class="num">Construction in (sqft)</th>
                <th rowspan="2" class="num">Project Budget ₹</th>
                <th rowspan="2" class="num">Utilized <br> ₹ (incl migration)</th>
                <th rowspan="2" class="num">Balance ₹</th>
              </tr>
              <tr>
                  <th class="num">Area</th>
                  <th class="num">₹ Rate</th>
              </tr>
            </thead>
            <tbody>
              <tr class="click" *ngFor="let g of groups()" (click)="openGroup(g.grp)">
                <td class="strong">{{g.desc}} </td>
                <td class="num">{{ area() ? (area()|number) : '—' }}</td>
                <td class="num">{{ g.rate!=null ? round(g.rate) : '—' }}</td>
                <td class="num strong">{{ g.budget | rup }}</td>
                <td class="num">{{ g.util | rup }}</td>
                <td class="num" [class.neg]="g.bal<0">{{ g.bal | rup }}</td>
              </tr>
            </tbody>
            <tfoot>
              <tr class="tfoot">
                <td>Grand Total</td>
                <td class="num">{{ area() ? (area() | number) : '—' }}</td>
                <td class="num">{{ projectTotals().rate != null ? round(projectTotals().rate) : '—' }}</td>
                <td class="num">{{ projectTotals().budget  | rup }}</td>
                <td class="num">{{ projectTotals().util  | rup }}</td>
                <td class="num" [class.neg]="projectTotals().bal < 0">{{ projectTotals().bal | rup }}</td>
              </tr>
            </tfoot>
          </table>
        </div>
        <div class="note" style="margin-top:10px" *ngIf="!area()">Per-SqFt rates are pending the project <b>saleable area</b>. Set <code>project.area</code> (SqFt) and the Area, Rate/SqFt and overall rate populate automatically.</div>       
      </cc-card>
      <div class="note"><b>Structure:</b> this is the budget-group summary (the Budget Approval view). Click any group to open its <b>9-series budget codes</b> (Budget A · WO/PO Issued B · Billed C · Billed w/o PO/WO or JV D · Balance E · Available F), then drill 9→7 series → WO/PO or billed transactions.</div>
    </ng-container>

    <!-- L2: 9-series of a group (same for both scopes) -->
    <ng-container *ngIf="ds.level()==='group'">
      <cc-backbar label="Budget Summary" (back)="ds.level.set('summary')"></cc-backbar>
      <div class="drill-head">{{group()}}</div>
      <div class="drill-sub">{{codes().length}} budget codes · ₹ Cr · click a code to drill 7-series</div>
      <cc-card>        
        <div class="tbl-scroll">
          <table class="compact">
            <thead>
              <tr>
                <th>Budget Code</th>
                <th>Budget <br>Description</th>
                <th class="num">Budget Amount<br>(A)</th>
                <th class="num">WO/PO Issued <br> (B)</th>
                <th class="num">WO/PO Billed <br> (C)</th>
                <th class="num">Direct Billed<br>(D)</th>
                <th class="num">WO/PO Balance <br> (E=B-C)</th>
                <th class="num">Unapproved WO/PO <br> (G)</th>
                <th class="num">Unposted Direct Expense <br> (H)</th>
                <th class="num">Budget Available <br> (F=A-B-D-G-H)</th>
              </tr>
            </thead>
            <tbody>
              <tr class="click" 
                  *ngFor="let b of codes()" 
               
                  [class.zero]="b.A === 0 && b.B === 0">
                <td class="mut" (click)="openCode(b)">
                  {{b.code}} 
                  <span *ngIf="hasBudgetTree(b.code)" class="drill-ic" title="Drill to 7-series">⤵</span>
                </td>
                <td class="strong" (click)="openCode(b)">{{b.desc}}</td>
                <td class="num">{{b.A | cr}}</td>
                <td class="num">
                  <span class="amt-link" (click)="budgetWO_9series(b.code,  b.desc)">
                      {{b.B | cr}}
                  </span>
                  <!-- {{b.B | cr}} -->
                </td>
                <td class="num">{{b.C | cr}}</td>
                <td class="num">
                   <span class="amt-link" (click)="budgetBilled_9series(b.code, b.desc)">
                       {{b.D | cr}} 
                  </span>
                  <!-- {{b.D | cr}} -->
                </td>
                <td class="num">{{b.E | cr}}</td>
                <td class="num">
                    <span class="amt-link" (click)="budgetWO_9series(b.code, b.desc)" title="View WO/PO detail lines">
                      <!-- {{b.B | rup}} -->
                        {{b.unappr}}
                    </span>
                  <!-- {{'XX'}} -->
                </td>
               <td class="num">
                    <span class="amt-link" (click)="budgetBilled_9series(b.code, b.desc)" title="View WO/PO detail lines">
                      <!-- {{b.B | rup}} -->
                        {{b.unposted}}
                    </span>
                  <!-- {{'XX'}} -->
                </td>

                <td class="num strong" [class.neg]="b.avail < 0">{{b.avail | cr}}</td>
              </tr>
            </tbody>
            <tfoot>
              <tr class="tfoot">
                <td colspan="2">Subtotal · {{groupTotals().count}} code{{groupTotals().count > 1 ? 's' : ''}}</td>
                <td class="num">{{groupTotals().A | cr}}</td>
                <td class="num">{{groupTotals().B | cr}}</td>
                <td class="num">{{groupTotals().C | cr}}</td>
                <td class="num">{{groupTotals().D | cr}}</td>
                <td class="num">{{groupTotals().E | cr}}</td>
                <td class="num"></td>
                <td class="num"></td>
                <td class="num strong">{{groupTotals().avail | cr}}</td>
              </tr>
            </tfoot>
          </table>
        </div>      
      </cc-card>
    </ng-container>

    <!-- L3: 7-series -->
    <ng-container *ngIf="ds.level()==='code'">
      <cc-backbar label="9-series codes" (back)="ds.level.set('group')"></cc-backbar>
      <div class="drill-head">Parent {{codeSel()?.code}} — {{codeSel()?.desc}}</div>
      <div class="drill-sub">7-series expense breakdown · ₹</div>
      
      <!-- KPI Row -->
      <!-- <div class="kpi-row">
        <cc-kpi
          cls="navy"
          [compact]="true"
          label="NS Budgeted (A)"
          [value]="'₹'+((codeSel()?.A ?? 0)|cr)"
          unit="Cr"
          sub="Budget">
        </cc-kpi>

        <cc-kpi
          [compact]="true"
          label="WO/PO Issued (B)"
          [value]="'₹'+((codeSel()?.B ?? 0)|cr)"
          unit="Cr"
          [sub]="getPercentageSubText(codeSel()?.B ?? 0, codeSel()?.A ?? 0)">
        </cc-kpi>

        <cc-kpi
          cls="good"
          [compact]="true"
          label="WO/PO Billed (C)"
          [value]="'₹'+((codeSel()?.C ?? 0)|cr)"
          unit="Cr"
          sub="Certified">
        </cc-kpi>

        <cc-kpi
          [cls]="(codeSel()?.avail ?? 0) < 0 ? 'bad' : 'warn'"
          [compact]="true"
          label="Budget Available (F)"
          [value]="'₹'+((codeSel()?.avail ?? 0)|cr)"
          unit="Cr"
          sub="A − B − E">
        </cc-kpi>
      </div> -->

      <cc-card>
        <div class="card-h">
          <h3>Expense codes (7-series)</h3>
          <span class="hint">Click an underlined WO/PO or Billed w/o PO/WO/JV amount to drill</span>
        </div>
        <div class="tbl-scroll">
          <table class="compact">
            <thead>
              <!-- <tr>
                <th>Expense Code</th>
                <th>Description</th>
                <th class="num">NS Budgeted (A)</th>
                <th class="num">WO/PO issued (B)</th>
                <th class="num">WO/PO Billed (C)</th>
                <th class="num">Billed w/o PO/WO/JV (D)</th>
                <th class="num">WO/PO Balance (E=B−C)</th>
                <th class="num">Budget Available (F=A−B−E)</th>
              </tr> -->
               <tr>
                <th>Expense Code</th>
                <th>Description</th>
                <th class="num">NS Budgeted<br>(A)</th>
                <th class="num">WO/PO Issued <br> (B)</th>
                <th class="num">WO/PO Billed <br> (C)</th>
                <th class="num">Direct Billed<br>(D)</th>
                <th class="num">WO/PO Balance <br> (E=B-C)</th>
                <th class="num">Unapproved WO/PO <br> (G)</th>
                <th class="num">Unposted Direct Expense <br> (H)</th>
                <th class="num">Budget Available <br> (F=A-B-D-G-H)</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let c of children()">
                <td class="mut">{{c.code}}</td>
                <td class="strong">{{c.desc}}</td>
                <td class="num">{{c.A | rup}}</td>
                <td class="num">
                  <span class="amt-link" (click)="budgetWO(codeSel()?.code, c.code, c.desc)" title="View WO/PO detail lines">{{c.B | rup}}</span>
                </td>
                <td class="num">{{c.C | rup}}</td>
                <td class="num">
                  <span class="amt-link" (click)="budgetBilled(codeSel()?.code, c.code, c.desc)" title="View transactions billed without PO/WO or JV">{{c.D | rup}}</span>   
                </td>
                <td class="num">{{c.E | rup}}</td>
                <td class="num">
                  <span class="amt-link" (click)="budgetWO(codeSel()?.code, c.code, c.desc)" title="View WO/PO detail lines">
                    {{c.B | rup}}
                  </span>                  
                </td>
                <td class="num">
                  <span class="amt-link" (click)="budgetBilled(codeSel()?.code, c.code, c.desc)" title="View WO/PO detail lines">
                     {{c.B | rup}}
                  </span>                  
                </td>
                <!-- <td class="num">XX</td> -->
                <td class="num strong" [class.neg]="c.F < 0">{{c.F | rup}}</td>
              </tr>
            </tbody>
            <tfoot>
              <tr class="tfoot">
                <td colspan="2">Total · {{childrenTotals().count}} code{{childrenTotals().count > 1 ? 's' : ''}}</td>
                <td class="num">{{childrenTotals().A | rup}}</td>
                <td class="num">{{childrenTotals().B | rup}}</td>
                <td class="num">{{childrenTotals().C | rup}}</td>
                <td class="num">{{childrenTotals().D | rup}}</td>
                <td class="num">{{childrenTotals().E | rup}}</td>
                <td class="num"></td>
                <td class="num"></td>
                <td class="num strong" [class.neg]="childrenTotals().F < 0">{{childrenTotals().F | rup}}</td>
              </tr>
            </tfoot>
          </table>

          <div class="note" *ngIf="children().length === 0">
            The 7-series expense breakdown for {{codeSel()?.code}} — {{codeSel()?.desc}} loads from the Budget Finance report. In this prototype the full drill (7-series → WO/PO details → Direct-Billed transactions) is mapped for <b>90101 Foundation</b>.
          </div>
          
          <div class="tbl-legend" *ngIf="children().length > 0">
            <b>Legend</b>
            <span class="li"><span class="amt-link">B</span> WO/PO details</span>
            <span class="li"><span class="amt-link">D</span> Transactions billed without PO/WO or JV</span>
            <span class="li mut">· amounts in ₹</span>
          </div>
        </div>      
      </cc-card>
    </ng-container>

    <!-- L4: WO/PO Details -->
    <ng-container *ngIf="ds.level() === 'wopo_details'">
      <cc-backbar [label]="backbarLabel()" (back)="ds.level.set('code')"></cc-backbar>
      
      <div class="drill-head">WO/PO Details for {{ wopoSel()?.childCode }}</div>
      <div class="drill-sub">{{ wopoDetails().length }}  lines · click back to return</div>
      
      <cc-card>
        <div class="card-h">
          <h3>WO/PO Detail Lines</h3>
          <span class="hint">Hover vendor / description for full text</span>
        </div>
        
        <div class="tbl-scroll">
          <table class="compact" *ngIf="wopoDetails().length > 0">
            <thead>
              <tr>
                <th>WO/PO No</th>
                <th>Date</th>
                <th>Status</th>
                <th>Vendor</th>
                <th>Item</th>
                <th>Description</th>
                <th>Unit</th>
                <th class="num">Qty</th>
                <th class="num">Rate</th>
                <th class="num">Amount</th>
                <th class="num">Billed Qty</th>
                <th class="num">Billed Amount</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let w of wopoDetails()">
                <td class="mut">{{ w.no }}</td>
                <td class="mut">{{ w.date }}</td>
                <td><span class="stat st-appr">{{ w.status }}</span></td>
                <td class=""><span class="trunc w170" title="{{ w.vendor }}">{{ w.vendor }}</span></td>
                <td class="mut">{{ w.item }}</td>
                <td><span class="trunc w220" title="{{ w.desc }}">{{ w.desc }}</span></td>
                <td class="mut">{{ w.unit }}</td>
                <td class="num">{{ w.qty | rup }}</td>
                <td class="num">{{ w.rate | rup }}</td>
                <td class="num strong">{{ w.amt | rup }}</td>
                <td class="num">{{ w.bqty | rup }}</td>
                <td class="num">{{ w.bamt | rup }}</td>
              </tr>
            </tbody>
            <tfoot>
              <tr class="tfoot">
                <td colspan="9">Total</td>
                <td class="num">{{ wopoTotalAmount() | rup }}</td>
                <td></td>
                <td class="num">{{ wopoTotalBilled() | rup }}</td>
              </tr>
            </tfoot>
          </table>
          
          <div class="note" *ngIf="wopoDetails().length === 0">
            No WO/PO details found for this code.
          </div>
        </div>
      </cc-card>
    </ng-container>

    <!-- L4: Billed without PO/WO or JV -->
    <ng-container *ngIf="ds.level() === 'wopojv'">
      <cc-backbar [label]="backbarLabelJV()" (back)="ds.level.set('code')"></cc-backbar>
      
      <div class="drill-head">Direct expeses without PO/WO or JV for {{ wopoBal()?.childCode }}</div>
      <div class="drill-sub">{{ billedDetails().length }} transactions · click back to return</div>
      
      <cc-card>
        <div class="card-h">
          <h3>Transactions billed without PO/WO or JV</h3>
          <span class="hint">Direct bill transactions mapped to this expense code</span>
        </div>
        
        <div class="tbl-scroll">
          <table class="compact" *ngIf="billedDetails().length > 0">
            <thead>
              <tr>
                <th>Transaction No</th>
                <th>Approval Status</th>
                <th>Transaction Date</th>
                <th>Account Description</th>
                <th class="num">Billed Amount</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let t of billedDetails()">
                <td class="mut">{{ t.no }}</td>
                <td><span class="stat st-appr">{{ t.status }}</span></td>
                <td class="mut">{{ t.date }}</td>
                <td><span class="trunc w220" title="{{ t.acct }}">{{ t.acct }}</span></td>
                <td class="num" [class.neg]="t.amt < 0">{{ t.amt | rup }}</td>
              </tr>
            </tbody>
            <tfoot>
              <tr class="tfoot">
                <td colspan="4">Total</td>
                <td class="num">{{ billedTotalAmount() | rup }}</td>
              </tr>
            </tfoot>
          </table>
          
          <div class="note" *ngIf="billedDetails().length === 0">
            No transactions Direct expenses without PO/WO or JV recorded against this code.
          </div>
        </div>
      </cc-card>
    </ng-container>

       <!-- L4: WO/PO Details -->
    <ng-container *ngIf="ds.level() === 'wopo_details_9ser'">
      <cc-backbar [label]="backbarLabel_9ser()" (back)="ds.level.set('group')"></cc-backbar>
      
      <div class="drill-head">WO/PO Details for {{ wopoSel()?.childCode }}</div>
      <div class="drill-sub">{{ wopoDetails_9series().length }}  lines · click back to return</div>
      
      <cc-card>
        <div class="card-h">
          <h3>WO/PO Detail Lines</h3>
          <span class="hint">Hover vendor / description for full text</span>
        </div>
        
        <div class="tbl-scroll">
          <table class="compact" *ngIf="wopoDetails_9series().length > 0">
            <thead>
              <tr>
                <th>WO/PO No</th>
                <th>Date</th>
                <th>Status</th>
                <th>Vendor</th>
                <th>Item</th>
                <th>Description</th>
                <th>Unit</th>
                <th class="num">Qty</th>
                <th class="num">Rate</th>
                <th class="num">Amount</th>
                <th class="num">Billed Qty</th>
                <th class="num">Billed Amount</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let w of wopoDetails_9series()">
                <td class="mut">{{ w.no }}</td>
                <td class="mut">{{ w.date }}</td>
                <td><span class="stat st-appr">{{ w.status }}</span></td>
                <td class=""><span class="trunc w170" title="{{ w.vendor }}">{{ w.vendor }}</span></td>
                <td class="mut">{{ w.item }}</td>
                <td><span class="trunc w220" title="{{ w.desc }}">{{ w.desc }}</span></td>
                <td class="mut">{{ w.unit }}</td>
                <td class="num">{{ w.qty | rup }}</td>
                <td class="num">{{ w.rate | rup }}</td>
                <td class="num strong">{{ w.amt | rup }}</td>
                <td class="num">{{ w.bqty | rup }}</td>
                <td class="num">{{ w.bamt | rup }}</td>
              </tr>
            </tbody>
            <tfoot>
              <tr class="tfoot">
                <td colspan="9">Total</td>
                <td class="num">{{ wopoTotalAmount() | rup }}</td>
                <td></td>
                <td class="num">{{ wopoTotalBilled() | rup }}</td>
              </tr>
            </tfoot>
          </table>
          
          <div class="note" *ngIf="wopoDetails().length === 0">
            No WO/PO details found for this code.
          </div>
        </div>
      </cc-card>
    </ng-container>

    <!-- L4: Billed without PO/WO or JV -->
    <ng-container *ngIf="ds.level() === 'wopojv_9series'">
      <cc-backbar [label]="backbarLabelJV_9ser()" (back)="ds.level.set('group')"></cc-backbar>
      
      <div class="drill-head">Direct expenses without PO/WO or JV for {{ wopoBal_9ser()?.parentCode }}</div>
      <div class="drill-sub">{{ billedDetails_9series().length }} transactions · click back to return</div>
      
      <cc-card>
        <div class="card-h">
          <h3>Transactions billed without PO/WO or JV</h3>
          <span class="hint">Direct bill transactions mapped to this expense code</span>
        </div>
        
        <div class="tbl-scroll">
          <table class="compact" *ngIf="billedDetails_9series().length > 0">
            <thead>
              <tr>
                <th>Transaction No</th>
                <th>Approval Status</th>
                <th>Transaction Date</th>
                <th>Account Description</th>
                <th class="num">Billed Amount</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let t of billedDetails_9series()">
                <td class="mut">{{ t.no }}</td>
                <td><span class="stat st-appr">{{ t.status }}</span></td>
                <td class="mut">{{ t.date }}</td>
                <td><span class="trunc w220" title="{{ t.acct }}">{{ t.acct }}</span></td>
                <td class="num" [class.neg]="t.amt < 0">{{ t.amt | rup }}</td>
              </tr>
            </tbody>
            <tfoot>
              <tr class="tfoot">
                <td colspan="4">Total</td>
                <td class="num">{{ billedTotalAmount() | rup }}</td>
              </tr>
            </tfoot>
          </table>
          
          <div class="note" *ngIf="billedDetails_9series().length === 0">
            No transactions Direct expenses without PO/WO or JV recorded against this code.
          </div>
        </div>
      </cc-card>
    </ng-container>
  `,
  styles: [`
    .mini-bar { height:6px; background:var(--bg); border-radius:4px; overflow:hidden; }
    .mini-bar span { display:block; height:100%; border-radius:4px; transition:width .3s; }
    .samp { background:#fef3c7; color:#b45309; font-size:9px; padding:1px 6px; border-radius:4px; margin-left:6px; font-weight:600; }
    .tfoot { background:var(--bg); font-weight:700; }
    .click { cursor:pointer; transition:background .15s; }
    .click:hover { background:var(--hover); }
    .zero td { color: #8A8E96;}
    .center { font-family: var(--mono); text-align: center;}
  `]
})
export class BudgetComponent {
  ds = inject(DataService);
  group = signal<string>('');
  codeSel = signal<BudgetRow | null>(null);
  wopoSel = signal<{
    parentCode: string;
    childCode: string;
    desc: string
  } | null>(null);


  wopoSel_9series = signal<{
    parentCode: number;
    children_series: [] | any;
    desc: string
  } | null>(null);


  wopoBal = signal<{
    parentCode: string;
    childCode: string;
    desc: string
  } | null>(null);

  wopoBal_9ser = signal<{
    parentCode: string;
    children_series: [] | any;
    desc: string
  } | null>(null);

  d = computed(() => this.ds.current());
  area = computed(() => this.d()?.project?.area ?? null);

  // Portfolio-level data for consolidated view
  projectSummaries = computed<ProjectAgg[]>(() => {
    const projects = this.ds.projects();

    return projects.map(p => ({
      id: p.id,
      name: p.name,
      stage: p.stage || '--',
      sample: !p.isReal,
      budget: p.budget,
      committed: p.committed,
      billed: p.billed,
      available: p.available,
      pct: p.budget > 0 ? Math.round((p.committed / p.budget) * 100) : 0
    }));
  });

  wopoDetails_9series = computed<WopoDetail[]>(() => {
    const children_series = this.wopoSel_9series()?.children_series;
    if (!children_series || children_series.length === 0) return [];

    const woDetails = this.d()?.woDetails;
    if (!woDetails || woDetails.length === 0) return [];


    // Convert selection to a Set for faster lookup
    const selectedSet = new Set(children_series.map((id: any) => String(id)));


    // Filter woDetails by matching `code`
    const result: WopoDetail[] = woDetails.filter(item =>
      selectedSet.has(String(item.code))
    );

    return result;
  });



  wopoDetails = computed<WopoDetail[]>(() => {
    const selection = this.wopoSel();

    if (!selection) return [];

    const d = this.d();
    if (!d) return [];

    const result: WopoDetail[] = [];
    const targetCode = String(selection.childCode).trim();


    if (d.woDetails && Array.isArray(d.woDetails) && d.woDetails.length > 0) {
      for (const detail of d.woDetails) {
        if (detail.code === undefined || detail.code === null) continue;
        const detailCode = String(detail.code).trim();
        if (detailCode !== targetCode) continue;

        result.push({
          no: detail.no || '',
          date: detail.date || '',
          status: detail.status || '',
          vendor: detail.vendor || '',
          item: detail.item || '',
          desc: detail.desc || '',
          unit: detail.unit || '',
          qty: detail.qty || 0,
          rate: detail.rate || 0,
          amt: detail.amt || 0,
          bqty: detail.bqty || 0,
          bamt: detail.bamt || 0,
          code: detail.code || ''
        });
      }
    }

    return result;
  });

  wopoTotalAmount = computed(() =>
    this.wopoDetails().reduce((sum, w) => sum + (w.amt ?? 0), 0)
  );

  wopoTotalBilled = computed(() =>
    this.wopoDetails().reduce((sum, w) => sum + (w.bamt ?? 0), 0)
  );

  wopojvDetails = computed<[]>(() => {
    return [];
  });

  // Portfolio totals
  portfolioTotal = (key: 'budget' | 'committed' | 'billed' | 'available') => {
    return this.projectSummaries().reduce((sum, p) => sum + p[key], 0);
  };

  // Project-level group aggregates
  // groups = computed<GroupAgg[]>(() => {
  //   const d = this.d();

  //   if (!d || this.ds.scope() === 'entity') return [];

  //   const m: Record<string, GroupAgg> = {};


  //   for (const b of d.budget) {
  //     let g = m[b.grp];
  //     if (!g) {
  //       g = { grp: b.grp, budget: 0, util: 0, bal: 0, rate: null };
  //       m[b.grp] = g;
  //     }
  //     g.budget += b.A;
  //     g.util += (b.C + b.D);
  //   }

  //     console.log('d.budget: ', d.budget);
  //   // console.log('d.budget: ', g.budget);

  //   const a = this.area();
  //   return Object.values(m).map(g => ({
  //     ...g,
  //     bal: g.budget - g.util,
  //     rate: a ? g.budget / a : null,
  //   }));
  // });

  groups = computed<GroupAgg[]>(() => {
    const d = this.d();
    if (!d || this.ds.scope() === 'entity') return [];

    const a = this.area();
    return d.budget.map(b => ({
      grp: b.grp,
      desc: b.desc,
      budget: b.A,
      util: b.C + b.D,
      bal: b.A - (b.C + b.D),
      rate: a ? b.A / a : null,
    }));
  });

  groupTotals = computed(() => {
    const codes = this.codes();
    if (!codes || codes.length === 0) {
      return {
        count: 0,
        A: 0,
        B: 0,
        C: 0,
        D: 0,
        E: 0,
        avail: 0
      };
    }

    return codes.reduce((acc, b) => ({
      count: acc.count + 1,
      A: acc.A + (b.A || 0),
      B: acc.B + (b.B || 0),
      C: acc.C + (b.C || 0),
      D: acc.D + (b.D || 0),
      E: acc.E + (b.E || 0),
      avail: acc.avail + (b.avail || 0)
    }), {
      count: 0,
      A: 0,
      B: 0,
      C: 0,
      D: 0,
      E: 0,
      avail: 0
    });
  });

  projectTotals = computed(() => {
    const groups = this.groups();
    const area = this.area();

    if (!groups || groups.length === 0) {
      return {
        count: 0,
        budget: 0,
        util: 0,
        bal: 0,
        area: area || 0,
        rate: 0
      };
    }

    const totals = groups.reduce((acc, g) => ({
      budget: acc.budget + g.budget,
      util: acc.util + g.util,
      bal: acc.bal + g.bal
    }), {
      budget: 0,
      util: 0,
      bal: 0
    });

    const oRate = area ? totals.budget * 1e7 / area : 0;

    return {
      ...totals,
      count: groups.length,
      area: area || 0,
      rate: oRate
    };
  });

  tB = computed(() => this.groups().reduce((s, g) => s + g.budget, 0));
  tU = computed(() => this.groups().reduce((s, g) => s + g.util, 0));
  tBal = computed(() => this.tB() - this.tU());
  oRate = computed(() => this.area() ? this.tB() * 1e7 / this.area()! : 0);

  codes = computed(() => (this.d()?.budget ?? []).filter(b => b.grp === this.group()));

  children = computed(() => {


    const selected = this.codeSel();
    if (!selected) return [];

    const treeData = this.d()?.budgetTree?.[selected.code];
    if (treeData?.children && treeData.children.length > 0) {
      return treeData.children;
    }

    const allBudget = this.d()?.budget || [];
    const hasSubCodes = allBudget.some(b => b.code !== selected.code && b.grp === selected.grp);

    if (hasSubCodes) {
      return [{
        code: 'No 7-series data',
        desc: 'No detailed breakdown available for this code',
        A: 0,
        B: 0,
        C: 0,
        D: 0,
        E: 0,
        F: 0
      }];
    }

    return [];
  });

  childrenTotals = computed(() => {
    const children = this.children();

    if (!children || children.length === 0) {
      return {
        count: 0,
        A: 0,
        B: 0,
        C: 0,
        D: 0,
        E: 0,
        F: 0
      };
    }

    return children.reduce((acc, c) => ({
      count: acc.count + 1,
      A: acc.A + (c.A || 0),
      B: acc.B + (c.B || 0),
      C: acc.C + (c.C || 0),
      D: acc.D + (c.D || 0),
      E: acc.E + (c.E || 0),
      F: acc.F + (c.F || 0)
    }), {
      count: 0,
      A: 0,
      B: 0,
      C: 0,
      D: 0,
      E: 0,
      F: 0
    });
  });

  round(n: number) {
    return Math.round(n).toLocaleString('en-IN');
  }

  openProject(projectId: string) {
    this.ds.setProject(projectId);
    if (this.ds.scope() === 'entity') {
      this.ds.setScope('project');
    }
  }

  openGroup(g: string) {
    this.group.set(g);
    this.ds.level.set('group');
  }

  openCode(b: BudgetRow) {
    this.codeSel.set(b);
    const hasChildren = (this.d()?.budgetTree?.[b.code]?.children?.length ?? 0) > 0;
    this.ds.level.set(hasChildren ? 'code' : 'code');
  }

  budgetBilled(parentCode: string | undefined, childCode: string, desc?: string) {

    console.log('parentCodess: ', parentCode, 'childCodess: ', childCode, 'descss: ', desc)

    this.wopoBal.set({
      parentCode: parentCode || '',
      childCode: childCode,
      desc: desc || ''
    });
    this.ds.level.set('wopojv');
  }

  budgetWO(
    parentCode: string | undefined,
    childCode: string,
    desc?: string,
    event?: MouseEvent
  ) {
    console.log('parentCodess: ', parentCode, 'childCodess: ', childCode, 'descss: ', desc, event)

    //console.log('parentCode: ', parentCode, 'childCode: ', childCode, 'desc: ', desc, 'event: ', event);
    event?.stopPropagation();

    if (!parentCode) {
      console.warn('Parent 9-series code is missing');
      return;
    }

    this.wopoSel.set({
      parentCode,
      childCode,
      desc: desc || ''
    });

    this.ds.level.set('wopo_details');
  }

  budgetBilled_9series(parentCode: any, desc: string) {

    if (!parentCode) {
      console.warn('Parent 9-series code is missing');
      return;
    }

    const selected_9_series_code = parentCode;
    const treeData = this.d()?.budgetTree?.[Number(selected_9_series_code)];

    // Normalize to strings, and default to [] to avoid .map() crash
    const children_series: string[] = (treeData?.children ?? []).map((s) =>
      String(s.code)
    );

    event?.stopPropagation();

    this.wopoBal_9ser.set({
      parentCode,
      children_series,
      desc
    });
    this.ds.level.set('wopojv_9series');
  }




  budgetWO_9series(parentCode: any,
    desc: string,
    event?: MouseEvent) {

    const selected_9_series_code = parentCode//this.codeSel();

    if (!selected_9_series_code) return [];

    const treeData = this.d()?.budgetTree?.[Number(selected_9_series_code)];
    const children_series = treeData?.children.map((s) => s.code)
    event?.stopPropagation();

    if (!parentCode) {
      console.warn('Parent 9-series code is missing');
      return;
    }

    this.wopoSel_9series.set({
      parentCode,
      children_series,
      desc
    });

    this.ds.level.set('wopo_details_9ser');
  }

  backbarLabel = computed(() => {
    const sel = this.wopoSel();
    if (!sel) return '7-series codes';
    return sel.desc ? `${sel.childCode} - ${sel.desc}` : `7-series codes (${sel.childCode})`;
  });

  backbarLabel_9ser = computed(() => {
    const sel = this.wopoSel_9series();
    if (!sel) return '9-series codes';
    return sel.desc ? `${sel.parentCode} - ${sel.desc}` : `7-series codes (${sel.parentCode})`;
  });

  billedDetails = computed<BilledDetail[]>(() => {
    const selection = this.wopoBal();
    if (!selection) return [];

    const d = this.d();

    if (!d) return [];

    const result: BilledDetail[] = [];
    const targetCode = String(selection.childCode).trim();


    if (!d.billedDetails) return [];

    if (typeof d.billedDetails === 'object' && !Array.isArray(d.billedDetails)) {
      const billedDetailsObj = d.billedDetails as Record<string, BilledDetail[]>;

      if (billedDetailsObj[targetCode]) {
        return billedDetailsObj[targetCode];
      }

      for (const [code, transactions] of Object.entries(billedDetailsObj)) {
        if (String(code).trim() === targetCode) {
          return transactions;
        }
      }

      return [];
    }

    if (Array.isArray(d.billedDetails)) {
      const matchedGroup = (d.billedDetails as any[]).find(
        item => item.code && String(item.code).trim() === targetCode
      );

      if (matchedGroup && Array.isArray(matchedGroup.transactions)) {
        return matchedGroup.transactions;
      }

      const filteredTransactions = (d.billedDetails as any[]).filter(
        item => item.code && String(item.code).trim() === targetCode
      );

      if (filteredTransactions.length > 0) {
        return filteredTransactions as BilledDetail[];
      }
    }

    return result;
  });

  billedDetails_9series = computed<BilledDetail[]>(() => {
    const selection = this.wopoBal_9ser();

    if (!selection) return [];

    const d = this.d();

    if (!d) return [];

    const children_series: string[] = (selection?.children_series ?? []).map((s: any) => String(s));

    if (children_series.length === 0) return [];

    const treeData_billedDetails = d.billedDetails ?? {};

    const results: BilledDetail[] = children_series.flatMap(code => treeData_billedDetails[Number(code)] ?? []);

    return results;
  });

  billedTotalAmount = computed(() =>
    this.billedDetails().reduce((sum, t) => sum + (t.amt ?? 0), 0)
  );

  backbarLabelJV = computed(() => {
    const sel = this.wopoBal();
    if (!sel) return '7-series codes';
    return sel.desc ? `${sel.childCode} - ${sel.desc}` : `7-series codes (${sel.childCode})`;
  });

  backbarLabelJV_9ser = computed(() => {
    const sel = this.wopoBal_9ser();
    if (!sel) return '9-series codes';
    return sel.desc ? `${sel.parentCode} - ${sel.desc}` : `9-series codes `;
  });

  private cr(value: number): string {
    return value?.toFixed(2) || '0';
  }

  private getPercentageColor(percentage: number): string {
    if (percentage <= 30) return 'c';
    if (percentage <= 60) return 'y';
    if (percentage <= 80) return 'g';
    return 'b';
  }

  private kpi(cls: string, label: string, value: string, unit: string, sub: string, bar?: number): string {
    return `<cc-kpi cls="${cls}" label="${label}" value="${value}" unit="${unit}" sub="${sub}"${bar ? ` bar="${bar}"` : ''}></cc-kpi>`;
  }

  getPercentageSubText(part: number, total: number): string {
    const percentage = total > 0
      ? Math.round((part / total) * 100)
      : 0;

    let colorClass = 'g';

    if (percentage <= 30) {
      colorClass = 'g';
    } else if (percentage <= 60) {
      colorClass = 'n';
    } else if (percentage <= 80) {
      colorClass = 'w';
    } else {
      colorClass = 'b';
    }

    return `<span class="tag ${colorClass}">${percentage}%</span>`;
  }

  hasBudgetTree(code: string): boolean {
    const treeData = this.d()?.budgetTree?.[code];
    return !!(treeData && treeData.children && treeData.children.length > 0);
  }

  getUtilizedSubText(): string {
    const percentage = this.tB() > 0
      ? Math.round((this.tU() / this.tB()) * 100)
      : 0;

    const colorClass = this.getPercentageColor(percentage);

    return `<span class="tag ${colorClass}">${percentage}%</span> incl migration`;
  }
}