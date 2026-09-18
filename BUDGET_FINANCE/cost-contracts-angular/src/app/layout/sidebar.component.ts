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

    <div class="sb-section" *ngIf="docked">Cockpit</div>
    <a class="nav-item" routerLink="/overview" routerLinkActive="active" (click)="ds.setScope('entity')" title="Overview">
      <span class="nav-ic">▦</span>
      <span *ngIf="docked">Overview</span>
    </a>
    <a class="nav-item" routerLink="/budget" routerLinkActive="active" title="Budget & Finance">
      <span class="nav-ic">▤</span>
      <span *ngIf="docked">Budget & Finance</span>
    </a>
   <a class="nav-item" routerLink="/boq" routerLinkActive="active"><span class="nav-ic">⇄</span>BOQ Comparison</a>
     <!-- <div class="sb-section">Procure to Pay</div> -->
    <!-- <a class="nav-item" routerLink="/vendors" routerLinkActive="active"><span class="nav-ic">◆</span>Contractors</a> -->
    <!-- <a class="nav-item" routerLink="/work-orders" routerLinkActive="active"><span class="nav-ic">▭</span>Work Orders</a>
    <a class="nav-item" routerLink="/purchase-orders" routerLinkActive="active"><span class="nav-ic">▥</span>Purchase Orders</a>
    <a class="nav-item" routerLink="/asn-grn" routerLinkActive="active"><span class="nav-ic">▧</span>ASN / GRN</a>
    <a class="nav-item" routerLink="/invoices" routerLinkActive="active"><span class="nav-ic">▦</span>Invoices & RA</a>
    <div class="sb-section">Controls</div>
    <a class="nav-item" routerLink="/risk" routerLinkActive="active"><span class="nav-ic">⚠</span>Risk Factors</a> -->
    <div class="sb-foot">
      <div *ngIf="docked"><span class="live-dot"></span><b>Live Sync</b></div>
      <div *ngIf="docked">NetSuite ERP · SuiteQL</div>
      <div *ngIf="!docked" title="Live Sync: NetSuite ERP · SuiteQL"><span class="live-dot"></span></div>
    </div>
  </aside>`
})
export class SidebarComponent {
  ds = inject(DataService);
  @Input() docked: boolean = true;
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
