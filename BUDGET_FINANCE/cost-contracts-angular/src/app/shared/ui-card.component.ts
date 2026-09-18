import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
@Component({
  selector:'cc-card', standalone:true, imports:[CommonModule],
  template:`
  <div class="card">
    <div class="card-h" *ngIf="title"><h3>{{title}}</h3><span class="hint" *ngIf="hint">{{hint}}</span></div>
    <ng-content></ng-content>
  </div>`
})
export class UiCardComponent { @Input() title=''; @Input() hint=''; }
