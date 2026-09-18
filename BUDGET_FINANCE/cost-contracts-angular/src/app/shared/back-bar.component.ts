import { Component, EventEmitter, Input, Output } from '@angular/core';
@Component({
  selector:'cc-backbar', standalone:true,
  template:`<div class="backbar" (click)="back.emit()">← {{label}}</div>`
})
export class BackBarComponent {
   @Input() label='Back'; @Output() back=new EventEmitter<void>(); 
  }
