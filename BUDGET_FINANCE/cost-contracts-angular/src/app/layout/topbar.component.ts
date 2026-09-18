import { Component, computed, inject, signal, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DataService } from '../core/services/data.service';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs';

@Component({
  selector: 'cc-topbar',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
  <div class="topbar">
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
    <div class="proj">
      <div class="lbl" id="scopeLbl" [class.disabled]="isProjectSelectorDisabled">
        <div class="lbl" id="scopeLbl">LEGAL ENTITY</div>
        <select class="proj-pick" id="projPick" [ngModel]="ds.projectId()" 
                (ngModelChange)="onProjectChange($event)"
                [disabled]="isProjectSelectorDisabled">
            <option *ngFor="let p of ds.projects()" [value]="p.id">
              {{p.name}}
            </option>
        </select>
      </div>
    </div>    
    <span class="chip live" id="stageChip">
      <span class="live-dot" style="animation:none"></span>
      {{ ds.scope() === 'project' ? selectedStage() : 'All projects' }}
    </span>
    <span class="chip">FY 2026-27 · YTD</span>

    <div class="spacer"></div>
    <div class="entity-name">{{ds.entity()?.name}}<span>{{ds.entity()?.group}}</span></div>
  </div>`,
  styles: [`
    .topbar{display:flex;align-items:center;gap:16px;background:#fff;border-bottom:1px solid var(--line);padding:11px 26px;position:sticky;top:0;z-index:30}
    .scope-toggle{display:flex;background:var(--bg);border-radius:9px;padding:3px}
    .scope-toggle button{border:none;background:none;font:inherit;font-weight:700;font-size:12px;padding:6px 14px;border-radius:7px;cursor:pointer;color:var(--t2)}
    .scope-toggle button:disabled, .proj-pick select:disabled { cursor: not-allowed; }
    .proj-pick.disabled, .scope-toggle.disabled { opacity: 0.6; pointer-events: none; } 
    .scope-toggle button.on{background:var(--navy);color:#fff}
    .proj-pick label{font-size:9px;letter-spacing:.5px;color:var(--t3);display:block}
    .proj-pick select{font:inherit;font-weight:600;border:1px solid var(--line);border-radius:8px;padding:6px 10px;background:#fff}
    .spacer{flex:1}.entity-name{text-align:right;font-weight:700;font-size:12px}.entity-name span{display:block;color:var(--t3);font-weight:400}
    `
  ]
})
export class TopbarComponent {
  ds = inject(DataService);
  private router = inject(Router);

  isOverviewRoute = signal(false);

  // Computed signals for better reactivity
  get isToggleDisabled() {
    return this.isOverviewRoute() || this.ds.scope() === 'entity';
  }

  get isProjectSelectorDisabled() {
    return this.isOverviewRoute() || this.ds.scope() === 'entity';
  }

  // Computed signal that automatically updates when projects or projectId changes
  selectedStage = computed(() => {
    const projects = this.ds.projects();

    if (!projects || projects.length === 0) return '';

    const currentProjectId = this.ds.projectId();
    const currentProject = projects.find((p: any) => p.id === currentProjectId);

    return currentProject?.stage || projects[0]?.stage || '';
  });

  constructor() {
    this.isOverviewRoute.set(this.router.url.includes('/overview'));

    // Optional: Log when selectedStage changes (for debugging)
    effect(() => { this.selectedStage() });

    // Listen to route changes
    this.router.events.pipe(
      filter(event => event instanceof NavigationEnd)
    ).subscribe(() => {
      this.isOverviewRoute.set(this.router.url.includes('/overview'));
    });
  }

  onProjectChange(projectId: string) {
    // This will trigger the computed signal to update automatically
    this.ds.setProject(projectId);

    const projects = this.ds.projects();
    if (projects && Array.isArray(projects)) {
      projects.find((p: any) => p.id === projectId);
    }
  }
}