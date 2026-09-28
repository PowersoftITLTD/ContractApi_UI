import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CurrencyUnitService } from '../core/services/currency-unit.service';

@Component({
  selector: 'cc-unit-selector',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="seg" role="group" aria-label="Units">
      <button type="button" data-unit="full" [class.on]="cus.unit() === 'full'" (click)="cus.setUnit('full')" title="Show in full Rupees (₹)">₹</button>
      <button type="button" data-unit="lakh" [class.on]="cus.unit() === 'lakh'" (click)="cus.setUnit('lakh')" title="Show in Lakhs">Lakhs</button>
      <button type="button" data-unit="cr" [class.on]="cus.unit() === 'cr'" (click)="cus.setUnit('cr')" title="Show in Crores">Crores</button>
    </div>
  `,
  styles: [`
    .seg {
      display: inline-flex;
      border: 1px solid var(--line, #e4dcc9);
      border-radius: 8px;
      overflow: hidden;
      background: var(--card, #ffffff);
      box-shadow: 0 1px 2px rgba(20,24,31,.04);
      vertical-align: middle;
    }
    .seg button {
      padding: 5px 12px;
      font-size: 11.5px;
      font-family: inherit;
      font-weight: 600;
      color: var(--t2, #5a5f69);
      background: none;
      border: none;
      border-right: 1px solid var(--line, #e4dcc9);
      cursor: pointer;
      transition: background 0.15s ease, color 0.15s ease;
      line-height: 1.3;
      user-select: none;
    }
    .seg button:last-child {
      border-right: none;
    }
    .seg button:hover {
      background: var(--surface2, #fbf8f2);
      color: var(--navy, #0e2a2b);
    }
    .seg button.on {
      background: var(--navy, #0e2a2b);
      color: #ffffff;
      font-weight: 700;
    }
  `]
})
export class UnitSelectorComponent {
  cus = inject(CurrencyUnitService);
}
