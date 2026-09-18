import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
@Component({
  selector:'cc-drawer', standalone:true, imports:[CommonModule],
  template:`
  <ng-container *ngIf="open">
    <div class="drawer-scrim" (click)="close.emit()"></div>
    <aside class="drawer">
      <div class="dr-head">
        <div class="dr-eyebrow">{{eyebrow}}</div>
        <div class="dr-title">{{title}}</div>
        <div class="dr-sub" *ngIf="subtitle">{{subtitle}}</div>
        <button class="dr-x" (click)="close.emit()">✕</button>
      </div>
      <div class="dr-tabs">
        <div class="dr-tab" *ngFor="let t of tabs" [class.on]="t===active" (click)="pick.emit(t)">{{t}}</div>
      </div>
      <div class="dr-body"><ng-content></ng-content></div>
    </aside>
  </ng-container>`,
  styles:[`.dr-head{padding:16px 20px;background:linear-gradient(120deg,var(--navy),var(--navy2));color:#fff;position:relative}
  .dr-eyebrow{font-size:10px;letter-spacing:.6px;text-transform:uppercase;opacity:.8}
  .dr-title{font-family:var(--serif);font-size:22px;margin-top:2px}
  .dr-sub{font-size:12px;opacity:.85;margin-top:3px}
  .dr-x{position:absolute;top:14px;right:16px;background:rgba(255,255,255,.15);border:none;color:#fff;width:26px;height:26px;border-radius:7px;cursor:pointer}`]
})
export class DrillDrawerComponent {
  @Input() open=false; @Input() eyebrow=''; @Input() title=''; @Input() subtitle='';
  @Input() tabs:string[]=[]; @Input() active='';
  @Output() close=new EventEmitter<void>(); @Output() pick=new EventEmitter<string>();
}
