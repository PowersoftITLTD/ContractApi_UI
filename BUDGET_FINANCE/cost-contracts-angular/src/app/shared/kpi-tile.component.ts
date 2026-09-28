import { Component, Input, inject, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CurrencyUnitService } from '../core/services/currency-unit.service';

@Component({
  selector: 'cc-kpi',
  standalone: true,
  imports: [CommonModule],
  template: `
  <div class="kpi" [class]="cls" [class.compact]="compact">
    <div class="k-lbl">{{label}}</div>
    <div class="k-val">{{displayValue()}}<span class="u" *ngIf="displayUnit()"> {{displayUnit()}}</span></div>
    <div class="k-sub" *ngIf="sub" [innerHTML]="sub"></div>
    <div class="k-bar" *ngIf="bar!=null"><span [style.width.%]="bar"></span></div>
  </div>`,
  styles: [`
    .kpi.compact {
      min-height: 0;
    }
  `]
})
export class KpiTileComponent {
  cus = inject(CurrencyUnitService);

  @Input() cls = '';
  @Input() label = '';
  @Input() value: any = '';
  @Input() unit = '';
  @Input() amount: number | null = null;
  @Input() sub: any = '';
  @Input() bar: number | null = null;
  @Input() subCls: any = '';
  @Input() compact = false;

  displayValue(): string {
    if (this.amount !== null && this.amount !== undefined) {
      return this.cus.rupee(this.amount, false);
    }
    return this.value;
  }

  displayUnit(): string {
    if (this.amount !== null && this.amount !== undefined) {
      return this.cus.kpiUnitLabel();
    }
    const u = (this.unit || '').trim();
    if (!u) return '';
    if (/^(cr|crore|crores)$/i.test(u)) {
      return this.cus.kpiUnitLabel();
    }
    return u;
  }
}
