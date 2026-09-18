import { Component, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { UiCardComponent } from '../shared/ui-card.component';
@Component({
  selector:'cc-stub', standalone:true, imports:[UiCardComponent],
  template:`
  <div class="page-title">{{title}}</div>
  <div class="page-sub">Scaffolded screen</div>
  <cc-card [title]="title" hint="follow the Work Orders pattern">
    <div class="note" style="padding:14px 16px">
      This feature is scaffolded. Build it like <b>Work Orders</b>: read <code>ds.current()</code>, render a
      <code>&lt;cc-card&gt;</code> table with <code>&lt;cc-sym&gt;</code> status symbols, and open a
      <code>&lt;cc-drawer&gt;</code> for the drill. Its data source is listed in <i>UI_Schema_Mapping.md</i>.
    </div>
  </cc-card>`
})
export class StubComponent { title = inject(ActivatedRoute).snapshot.data['title'] || 'Screen'; }
