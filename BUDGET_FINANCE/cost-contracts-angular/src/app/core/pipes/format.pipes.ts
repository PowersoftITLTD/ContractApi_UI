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


@Pipe({ name: 'distinctBy', standalone: true })
export class DistinctByPipe implements PipeTransform {
  transform<T>(arr: T[] | null, key: keyof T): T[] {
    if (!arr) return [];
    const seen = new Set<any>();
    return arr.filter(x => {
      const k = x[key];
      if (seen.has(k)) return false;
      seen.add(k);
      return true;
    });
  }
}

/** Filters budget rows while retaining a 9-series row when one of its 7-series
 * children matches. The optional budget rows let group summaries stay visible
 * when any nested code in that group matches. */
@Pipe({ name: 'budgetTreeSearch', standalone: true })
export class BudgetTreeSearchPipe implements PipeTransform {
  transform<T extends object>(
    rows: T[] | null | undefined,
    query: string,
    budgetTree?: Record<string, { children?: Array<{ code?: string | number; desc?: string }> }>,
    allBudgetRows?: Array<Record<string, any>>,
    nestedDetails?: Record<string, object[]>
  ): T[] {
    const list = rows ?? [];
    const term = (query ?? '').trim().toLocaleLowerCase();
    if (!term) return list;

    const matches = (row: object) =>
      Object.values(row).some(value =>
        value != null && String(value).toLocaleLowerCase().includes(term));
    const detailsMatch = (code: string | number | undefined) =>
      code != null && (nestedDetails?.[String(code)] ?? []).some(matches);
    const childrenMatch = (code: string | number) =>
      (budgetTree?.[String(code)]?.children ?? []).some(child =>
        matches(child) || detailsMatch(child.code));

    // Group aggregates include budget/util numbers; search their label and
    // retain a group when one of its nested codes/children matches.
    // console.log('budgetTree: ', budgetTree);
    if (list.length && 'grp' in list[0] && !('code' in list[0])) {
      return list.filter(group => {
        if (matches(group)) return true;
        return (allBudgetRows ?? []).some(row => row.grp === (group as any)['grp'] &&
          (matches(row) || childrenMatch((row as any).code)));
      });
    }

    return list.filter(row => matches(row) || childrenMatch((row as any)['code']) || detailsMatch((row as any)['code']));
  }
}
