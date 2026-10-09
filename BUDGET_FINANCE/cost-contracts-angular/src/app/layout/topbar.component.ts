import { Component, computed, inject, signal, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DataService } from '../core/services/data.service';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs';
import { UnitSelectorComponent } from '../shared/unit-selector.component';
import { SearchComponent } from '../shared/search.component';
import { Scope } from '../core/models/models';

@Component({
  selector: 'cc-topbar',
  standalone: true,
  imports: [CommonModule, FormsModule, UnitSelectorComponent, SearchComponent],
  template: `
  <div class="topbar">
    <!-- <div class="brand">
      <div class="brand-mark">
        <svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true">
          <path d="M4 7h16M4 12h10M4 17h7" stroke="currentColor" stroke-width="2"
                stroke-linecap="round" fill="none"/>
        </svg>
      </div>
    </div> -->

    <!-- <div class="divider"></div> -->

    <!-- Scope toggle -->
    <div class="scope-toggle" [class.disabled]="isToggleDisabled">
      <button [class.on]="ds.scope()==='entity'"
              (click)="ds.setScope('entity')"
              [disabled]="isToggleDisabled">
        Consolidated
      </button>
      <button [class.on]="ds.scope()==='project'"
              (click)="ds.setScope('project')"
              [disabled]="isToggleDisabled">
        Project
      </button>
    </div>

    <!-- Legal entity picker -->
    <div class="proj" [class.disabled]="isProjectSelectorDisabled">
      <label class="field-label" for="projPick">LEGAL ENTITY</label>
      <div class="select-wrap">
        <select id="projPick" class="proj-pick"
                [ngModel]="ds.projectId()"
                (ngModelChange)="onProjectChange($event)"
                [disabled]="isProjectSelectorDisabled">
          <option *ngFor="let p of ds.projects()" [value]="p.id">{{p.name}}</option>
        </select>
        <svg class="chev" viewBox="0 0 24 24" width="12" height="12" aria-hidden="true">
          <path d="M6 9l6 6 6-6" fill="none" stroke="currentColor"
                stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
        </svg>
      </div>
    </div>

    <!-- Chips -->
    <div class="chips">
      <span class="chip live" id="stageChip">
        <span class="live-dot"></span>
        {{ ds.scope() === 'project' ? selectedStage() : 'All projects' }}
      </span>
      <span class="chip subtle">FY 2026-27 · YTD</span>
    </div>

    <div class="spacer"></div>

    <!-- Right cluster -->
    <div class="actions">
      <cc-search *ngIf="!isToggleDisabled" (search)="handleSearch($event)"></cc-search>
      <cc-unit-selector></cc-unit-selector>
      <div class="divider"></div>
      <div class="entity-name">
        <span class="name">{{ds.entity()?.name}}</span>
        <span class="group">{{ds.entity()?.group}}</span>
      </div>
    </div>
  </div>`,
  styles: [`
    :host { display: block; }

    .topbar{
      display:flex;
      align-items:center;
      gap:14px;
      padding:0 22px;
      height:58px;
      background:#fff;
      border-bottom:1px solid var(--line);
      box-shadow:0 1px 0 rgba(20,30,60,.02), 0 6px 18px -14px rgba(20,30,60,.25);
      position:sticky;
      top:0;
      z-index:30;
    }

    /* Brand mark */
    .brand{ display:flex; align-items:center; }
    .brand-mark{
      width:30px; height:30px;
      display:grid; place-items:center;
      border-radius:9px;
      background:linear-gradient(135deg, var(--navy), #2b3f68);
      color:#fff;
      box-shadow:0 4px 10px -4px rgba(26,43,76,.5);
    }

    .divider{
      width:1px; height:22px;
      background:var(--line);
      flex:none;
    }

    /* Scope toggle */
    .scope-toggle{
      display:flex;
      background:var(--bg);
      border-radius:10px;
      padding:3px;
      border:1px solid var(--line);
    }
    .scope-toggle button{
      border:none; background:none; font:inherit;
      font-weight:700; font-size:12px;
      padding:6px 14px;
      border-radius:7px;
      cursor:pointer;
      color:var(--t2);
      transition:background .18s ease, color .18s ease, box-shadow .18s ease;
    }
    .scope-toggle button:hover:not(:disabled):not(.on){
      color:var(--navy);
    }
    .scope-toggle button.on{
      background:var(--navy);
      color:#fff;
      box-shadow:0 2px 6px -2px rgba(26,43,76,.5);
    }
    .scope-toggle button:disabled,
    .proj.disabled .proj-pick,
    .proj.disabled .field-label { cursor:not-allowed; }
    .scope-toggle.disabled,
    .proj.disabled { opacity:.55; }

    /* Legal entity picker */
    .proj{ display:flex; flex-direction:column; gap:2px; min-width:170px; }
    .field-label{
      font-size:9px;
      letter-spacing:.7px;
      font-weight:700;
      color:var(--t3);
      padding-left:2px;
    }
    .select-wrap{ position:relative; }
    .proj-pick{
      appearance:none;
      -webkit-appearance:none;
      font:inherit;
      font-weight:600;
      font-size:12.5px;
      color:var(--t1);
      border:1px solid var(--line);
      border-radius:9px;
      padding:6px 28px 6px 10px;
      background:#fff;
      width:100%;
      cursor:pointer;
      transition:border-color .15s ease, box-shadow .15s ease;
    }
    .proj-pick:hover:not(:disabled){ border-color:#c8cedb; }
    .proj-pick:focus{
      outline:none;
      border-color:var(--navy);
      box-shadow:0 0 0 3px rgba(26,43,76,.12);
    }
    .chev{
      position:absolute;
      right:9px; top:50%;
      transform:translateY(-50%);
      color:var(--t3);
      pointer-events:none;
    }

    /* Chips */
    .chips{ display:flex; align-items:center; gap:8px; }
    .chip{
      display:inline-flex;
      align-items:center;
      gap:6px;
      font-size:11.5px;
      font-weight:700;
      padding:5px 11px;
      border-radius:999px;
      border:1px solid var(--line);
      color:var(--t2);
      background:#fff;
      white-space:nowrap;
    }
    .chip.subtle{
      color:var(--t3);
      font-weight:600;
      background:var(--bg);
      border-color:transparent;
    }
    .chip.live{
      color:#0d8a5a;
      background:#e9f8f1;
      border-color:transparent;
    }
    .live-dot{
      width:6px; height:6px; border-radius:50%;
      background:#16b981;
      box-shadow:0 0 0 3px rgba(22,185,129,.18);
    }

    .spacer{ flex:1; min-width:8px; }

    /* Right cluster */
    .actions{
      display:flex;
      align-items:center;
      gap:14px;
    }
    .entity-name{
      display:flex;
      flex-direction:column;
      align-items:flex-end;
      line-height:1.15;
      max-width:180px;
    }
    .entity-name .name{
      font-weight:700;
      font-size:12.5px;
      color:var(--t1);
      white-space:nowrap;
      overflow:hidden;
      text-overflow:ellipsis;
      max-width:180px;
    }
    .entity-name .group{
      font-size:11px;
      color:var(--t3);
      white-space:nowrap;
      overflow:hidden;
      text-overflow:ellipsis;
      max-width:180px;
    }

    @media (max-width: 900px){
      .chips{ display:none; }
    }
    @media (max-width: 720px){
      .topbar{ gap:10px; padding:0 14px; }
      .proj{ min-width:130px; }
      .entity-name{ display:none; }
    }
  `]
})
export class TopbarComponent {
  ds = inject(DataService);
  private router = inject(Router);

  isOverviewRoute = signal(false);
  scope = signal<Scope>('entity');

  get isToggleDisabled() {
    return this.isOverviewRoute() || this.ds.scope() === 'entity';
  }

  get isProjectSelectorDisabled() {
    return this.isOverviewRoute() || this.ds.scope() === 'entity';
  }

  handleSearch(query: string) {
    this.ds.searchQuery.set(query);
  }

  selectedStage = computed(() => {
    const projects = this.ds.projects();
    if (!projects || projects.length === 0) return '';
    const currentProjectId = this.ds.projectId();
    const currentProject = projects.find((p: any) => p.id === currentProjectId);
    return currentProject?.stage || projects[0]?.stage || '';
  });

  constructor() {
    this.isOverviewRoute.set(this.router.url.includes('/overview'));
      this.syncScopeWithRoute();          // ← handle initial deep-link

    effect(() => { this.selectedStage(); });
    this.router.events.pipe(
      filter(event => event instanceof NavigationEnd)
    ).subscribe(() => {
      this.isOverviewRoute.set(this.router.url.includes('/overview'));
          this.syncScopeWithRoute();        // ← MISSING: run on every route change

    });
  }

  onProjectChange(projectId: string) {
    this.ds.setProject(projectId);
    const projects = this.ds.projects();
    if (projects && Array.isArray(projects)) {
      projects.find((p: any) => p.id === projectId);
    }
  }

  private syncScopeWithRoute() {
  if (this.isOverviewRoute() && this.ds.scope() !== 'entity') {
    this.ds.setScope('entity');
  }
}
}