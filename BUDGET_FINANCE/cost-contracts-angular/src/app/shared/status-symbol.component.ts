import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
type Kind = 'approval'|'billing'|'payment'|'asn';
@Component({
  selector:'cc-sym', standalone:true, imports:[CommonModule],
  template:`<span class="sym {{color}}" [title]="value||''">{{glyph}}</span>`
})
export class StatusSymbolComponent {
  @Input() kind:Kind='approval'; @Input() value:string|null='';
  get glyph(){ return this.map().g; } get color(){ return this.map().c; }
  private map(){
    const v=String(this.value||'');
    if(this.kind==='approval'){
      if(v==='Approved')return{g:'✓',c:'g'}; if(/Reject/.test(v))return{g:'✕',c:'b'}; return{g:'⧗',c:'v'};
    }
    if(this.kind==='asn'){
      if(v==='Approved')return{g:'✓',c:'g'}; if(/Process/.test(v))return{g:'⧗',c:'v'};
      if(/Cancel/.test(v))return{g:'⊘',c:'mut'}; if(/Reject/.test(v))return{g:'✕',c:'b'}; return{g:'—',c:'mut'};
    }
    if(this.kind==='payment'){
      if(/Paid/.test(v))return{g:'●',c:'g'}; if(/Open/.test(v))return{g:'◐',c:'w'};
      if(/Cancel/.test(v))return{g:'⊘',c:'mut'}; if(/Reject/.test(v))return{g:'✕',c:'b'}; return{g:'⧗',c:'v'};
    }
    // billing
    if(/Fully Billed/.test(v))return{g:'●',c:'g'}; if(/Closed/.test(v))return{g:'◉',c:'n'};
    if(/Partial/.test(v))return{g:'◑',c:'w'}; if(/Pending Bill|Pending Billing/.test(v))return{g:'◐',c:'w'};
    if(/Reject/.test(v))return{g:'✕',c:'b'}; return{g:'○',c:'mut'};
  }
}
