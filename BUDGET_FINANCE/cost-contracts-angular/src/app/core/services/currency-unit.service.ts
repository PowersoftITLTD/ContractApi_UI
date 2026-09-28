import { Injectable, signal, computed } from '@angular/core';

export type CurrencyUnit = 'full' | 'lakh' | 'cr';

@Injectable({ providedIn: 'root' })
export class CurrencyUnitService {
  /** Active unit: 'full' (₹), 'lakh' (Lakhs), 'cr' (Crores). Defaults to 'cr' */
  unit = signal<CurrencyUnit>('lakh');

  /** Label for the segmented button */
  unitLabel = computed(() => {
    switch (this.unit()) {
      case 'full': return '₹';
      case 'lakh': return 'Lakhs';
      case 'cr': return 'Crores';
    }
  });

  /** KPI suffix label: 'Cr' for crores, 'Lakhs' for lakhs, '' for full rupees */
  kpiUnitLabel = computed(() => {
    switch (this.unit()) {
      case 'full': return '';
      case 'lakh': return 'Lakhs';
      case 'cr': return 'Cr';
    }
  });

  /** Suffix to append to numbers when requested */
  unitSuffix = computed(() => {
    switch (this.unit()) {
      case 'full': return '';
      case 'lakh': return ' L';
      case 'cr': return ' Cr';
    }
  });

  /** Long title for headers, subtitles, or table footers */
  currencyTitle = computed(() => {
    switch (this.unit()) {
      case 'full': return '₹ (Full)';
      case 'lakh': return '₹ in Lakhs';
      case 'cr': return '₹ in Crores';
    }
  });

  unitTitle = computed(() => {
    switch (this.unit()) {
      case 'full': return 'Full';
      case 'lakh': return 'Lakhs';
      case 'cr': return 'Cr';
    }
  });

  setUnit(u: CurrencyUnit): void {
    this.unit.set(u);
  }

  /**
   * Convert raw rupee amount to numeric value in current unit
   */
  convert(n: number | null | undefined): number {
    const raw = Number(n) || 0;
    switch (this.unit()) {
      case 'full': return raw;
      case 'lakh': return raw / 1e5;
      case 'cr': return raw / 1e7;
    }
  }

  /**
   * Format number using Indian comma grouping with optional suffix
   * Similar to the reference HTML's fmt(n)
   */
  fmt(n: number | null | undefined, withSuffix: boolean = false, decimals: number = 2): string {
    if (n === null || n === undefined) return '0.00';
    const num = Number(n);
    if (isNaN(num)) return '0.00';
    if (num === 0) return '0.00' + (withSuffix ? this.unitSuffix() : '');

    let v = num;
    let suf = '';
    const currentUnit = this.unit();
    if (currentUnit === 'lakh') {
      v = num / 1e5;
      if (withSuffix) suf = ' L';
    } else if (currentUnit === 'cr') {
      v = num / 1e7;
      if (withSuffix) suf = ' Cr';
    }

    const neg = v < 0;
    v = Math.abs(v);

    const [intPart, decPart] = v.toFixed(decimals).split('.');
    const last3 = intPart.slice(-3);
    let rest = intPart.slice(0, -3);
    if (rest) {
      rest = rest.replace(/\B(?=(\d{2})+(?!\d))/g, ',') + ',';
    }
    const formatted = (neg ? '-' : '') + rest + last3 + (decimals > 0 ? '.' + decPart : '') + suf;
    return formatted;
  }

  /**
   * Format number prefixed with ₹ symbol
   * Similar to the reference HTML's rupee(n)
   */
  rupee(n: number | null | undefined, withSuffix: boolean = false, decimals: number = 2): string {
    if (n === null || n === undefined) return '₹0.00';
    const num = Number(n);
    if (isNaN(num)) return '₹0.00';
    const neg = num < 0;
    const formatted = this.fmt(Math.abs(num), withSuffix, decimals);
    return (neg ? '-' : '') + '₹' + formatted;
  }

  /**
   * CSS class helper: 'neg' | 'zero' | 'pos'
   */
  cls(n: number | null | undefined): string {
    const num = Number(n) || 0;
    return num < 0 ? 'neg' : num === 0 ? 'zero' : 'pos';
  }
}
