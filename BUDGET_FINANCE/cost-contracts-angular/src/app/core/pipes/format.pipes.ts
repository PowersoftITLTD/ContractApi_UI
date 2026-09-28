import { Pipe, PipeTransform, inject } from '@angular/core';
import { CurrencyUnitService } from '../services/currency-unit.service';

/**
 * Unit-aware number formatter (default cr, switches to Lakhs / Full Rupee based on active unit).
 * Indian-grouped with 2 decimal places.
 */
@Pipe({ name: 'cr', standalone: true, pure: false })
export class CrPipe implements PipeTransform {
  private cus = inject(CurrencyUnitService);

  transform(n: number | null | undefined, withSuffix: boolean = false): string {
    return this.cus.fmt(n, withSuffix);
  }
}

/**
 * Currency pipe with symbol and unit suffix according to active unit.
 * Examples:
 *   {{ val | curr }}             => '₹53.11 Cr' or '₹5,311.00 L' or '₹53,11,00,000.00'
 *   {{ val | curr:false }}       => '53.11 Cr' or '5,311.00 L' or '53,11,00,000.00'
 *   {{ val | curr:true:false }}  => '₹53.11' or '₹5,311.00' or '₹53,11,00,000.00'
 *   {{ val | curr:false:false }} => '53.11' or '5,311.00' or '53,11,00,000.00'
 */
@Pipe({ name: 'curr', standalone: true, pure: false })
export class CurrPipe implements PipeTransform {
  private cus = inject(CurrencyUnitService);

  transform(n: number | null | undefined, withSymbol: boolean = true, withSuffix: boolean = true): string {
    if (withSymbol) {
      return this.cus.rupee(n, withSuffix);
    }
    return this.cus.fmt(n, withSuffix);
  }
}

/**
 * Unit-aware Rupee formatter with ₹ prefix.
 */
@Pipe({ name: 'rup', standalone: true, pure: false })
export class RupPipe implements PipeTransform {
  private cus = inject(CurrencyUnitService);

  transform(n: number | null | undefined, withSuffix: boolean = false): string {
    return this.cus.rupee(n, withSuffix);
  }
}

/**
 * Returns current unit label or title for dynamic table headers and labels.
 *   {{ 'kpi' | currUnit }}   => 'Cr' | 'Lakhs' | ''
 *   {{ 'title' | currUnit }} => '₹ in Crores' | '₹ in Lakhs' | '₹ (Full)'
 *   {{ 'label' | currUnit }} => 'Crores' | 'Lakhs' | '₹'
 */
@Pipe({ name: 'currUnit', standalone: true, pure: false })
export class CurrUnitPipe implements PipeTransform {
  private cus = inject(CurrencyUnitService);

  transform(type: 'label' | 'title' | 'suffix' | 'kpi' = 'kpi'): string {
    switch (type) {
      case 'label': return this.cus.unitLabel();
      case 'title': return this.cus.currencyTitle();
      case 'suffix': return this.cus.unitSuffix();
      case 'kpi': return this.cus.kpiUnitLabel();
    }
  }
}

/** Full raw ₹ with Indian grouping, 2dp. */
@Pipe({ name: 'inr', standalone: true })
export class InrPipe implements PipeTransform {
  transform(n: number | null | undefined): string {
    return (Number(n) || 0).toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  }
}

/** Percentage calculation: (part / whole) * 100 */
@Pipe({ name: 'pctOf', standalone: true })
export class PctOfPipe implements PipeTransform {
  transform(part: number, whole: number): number {
    return whole ? Math.round((part / whole) * 100) : 0;
  }
}
