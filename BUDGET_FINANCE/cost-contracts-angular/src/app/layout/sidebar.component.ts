import { Component, EventEmitter, inject, Input, Output, HostBinding } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { DataService } from '../core/services/data.service';

@Component({
  selector: 'cc-sidebar',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive],
  template: `
  <aside class="sidebar" [class.collapsed]="!docked">
    <div class="sb-brand">
      <div *ngIf="docked">
        <div class="sb-logo">Powersoft <em>MIS</em></div>
        <div class="sb-tag">Cost & Contracts · RealtyOne</div>
      </div>
      <div *ngIf="!docked" class="sb-logo-mini">P<em>MIS</em></div>
      <button type="button" class="sb-dock-btn" (click)="toggleDock()" [title]="docked ? 'Collapse sidebar' : 'Expand sidebar'">
        {{ docked ? '«' : '»' }}
      </button>
    </div>

    <div class="sb-section tooltip-container" *ngIf="docked">Cockpit</div>

    <!-- The custom tooltips only evaluate if the sidebar is collapsed (!docked) -->
     <a class="nav-item" routerLink="/overview" routerLinkActive="active" [attr.data-tooltip]="!docked ? 'Overview' : null">    
      <span class="nav-ic">▦</span>
      <span *ngIf="docked">Overview</span>
    </a>

    <a class="nav-item" routerLink="/budget" routerLinkActive="active" [attr.data-tooltip]="!docked ? 'Budget & Finance' : null">
      <span class="nav-ic">▤</span>
      <span *ngIf="docked">Budget & Finance</span>
    </a>
    
    <a class="nav-item" routerLink="/boq" routerLinkActive="active" [attr.data-tooltip]="!docked ? 'BOQ Comparison' : null">
      <span class="nav-ic">⇄</span>
      <span *ngIf="docked">BOQ Comparison</span>
    </a>

    <div class="sb-foot">
      <div *ngIf="docked"><span class="live-dot"></span><b>Live Sync</b></div>
      <div *ngIf="docked">NetSuite ERP · SuiteQL</div>
      <div *ngIf="!docked" title="Live Sync: NetSuite ERP · SuiteQL"><span class="live-dot"></span></div>
    </div>
  </aside>`,
  styles: [
    `
    /* 1. Establish the container layout */
    .nav-item {
      position: relative; 
      display: flex;
      align-items: center;
    }

    /* 2. Position the Custom Tooltip Box onto the RIGHT side of the sidebar */
    .nav-item[data-tooltip]::before {
      content: attr(data-tooltip);
      position: absolute;
      left: 105%;                  /* Pushes the tooltip to the right side of the nav button */
      top: 50%;
      transform: translateY(-50%) translateX(-5px); /* Centers vertically and pulls left slightly for animations */
      
      /* Modern Styling */
      background-color: #1e293b;
      color: #ffffff;
      padding: 6px 12px;
      border-radius: 6px;
      font-size: 13px;
      font-weight: 500;
      white-space: nowrap;
      box-shadow: 0 4px 6px -1px rgb(0 0 0 / 0.1), 0 2px 4px -2px rgb(0 0 0 / 0.1);
      
      /* Default hidden state */
      opacity: 0;
      pointer-events: none;
      transition: opacity 0.15s ease, transform 0.15s ease;
      z-index: 1;                /* High z-index to overlay nicely on main page content templates */
    }

    /* 3. Create a Left-Pointing Arrow Indicator */
    .nav-item[data-tooltip]::after {
      content: "";
      position: absolute;
      left: 105%;
      top: 50%;
      transform: translateY(-50%) translateX(-5px);
      margin-left: -5px;           /* Pulls the arrow right to the border boundary edge */
      
      border-width: 5px 5px 5px 0;
      border-style: solid;
      border-color: transparent #1e293b transparent transparent;
      
      opacity: 0;
      pointer-events: none;
      transition: opacity 0.15s ease, transform 0.15s ease;
      z-index: 999;
    }

    /* 4. Smooth slide out to the right side upon Hover */
    .nav-item[data-tooltip]:hover::before,
    .nav-item[data-tooltip]:hover::after {
      opacity: 1;
      transform: translateY(-50%) translateX(0);
    }
    `
  ]
})
export class SidebarComponent {
  ds = inject(DataService);
  @Input() docked: boolean = false;
  @Output() dockToggled = new EventEmitter<void>();

  @HostBinding('style.display') hostDisplay = 'block';
  @HostBinding('style.flexShrink') hostFlexShrink = '0';
  @HostBinding('style.width.px') get hostWidth(): number {
    return this.docked ? 230 : 60;
  }
  @HostBinding('style.transition') hostTransition = 'width 0.2s ease';

  toggleDock(): void {
    this.docked = !this.docked;
    this.dockToggled.emit();
  }
}
