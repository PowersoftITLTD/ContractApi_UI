import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
@Component({
  selector:'cc-subtabs', standalone:true, imports:[CommonModule],
  template:`
  <div class="subtabs">
    <button class="subtab" *ngFor="let t of tabs" [class.on]="t===active" (click)="pick.emit(t)">{{t}}</button>
  </div>`
})
export class SubTabsComponent {
  @Input() tabs:string[]=[]; @Input() active=''; @Output() pick=new EventEmitter<string>();
}
