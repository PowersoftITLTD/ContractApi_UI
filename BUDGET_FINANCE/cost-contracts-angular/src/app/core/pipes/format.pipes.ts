import { Pipe, PipeTransform } from '@angular/core';
/** ₹Cr style: number already in Crore -> Indian-grouped, 2dp. */
@Pipe({ name: 'cr', standalone: true })
export class CrPipe implements PipeTransform {
  transform(n: number | null | undefined): string {
    const crores = (Number(n) || 0) / 1_00_00_000;
    return crores.toFixed(2);
  }
}
/** Full ₹ with Indian grouping, 2dp. */
@Pipe({ name:'inr', standalone:true })
export class InrPipe implements PipeTransform {
  transform(n: number|null|undefined): string {
    return (Number(n)||0).toLocaleString('en-IN',{minimumFractionDigits:2,maximumFractionDigits:2});
  }
}
/** Full ₹ rounded (no decimals) with ₹ prefix — used on the budget summary. */
@Pipe({ name:'rup', standalone:true })
export class RupPipe implements PipeTransform {
  transform(n: number|null|undefined): string {
    return '₹'+Math.round(Number(n)||0).toLocaleString('en-IN');
  }
}
/** percentage of. */
@Pipe({ name:'pctOf', standalone:true })
export class PctOfPipe implements PipeTransform {
  transform(part:number, whole:number): number { return whole ? Math.round((part/whole)*100) : 0; }
}
