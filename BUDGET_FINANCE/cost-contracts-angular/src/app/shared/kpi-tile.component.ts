import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
@Component({
  selector:'cc-kpi', standalone:true, imports:[CommonModule],
  template:`
  <div class="kpi" [class]="cls" [class.compact]="compact">
    <div class="k-lbl">{{label}}</div>
    <div class="k-val">{{value}}<span class="u" *ngIf="unit"> {{unit}}</span></div>
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
  @Input() cls=''; @Input() label=''; @Input() value:any=''; @Input() unit='';
  @Input() sub:any='';
  @Input() bar:number|null=null;  
  @Input() subCls:any = '';
  @Input() compact = false;
  
}
