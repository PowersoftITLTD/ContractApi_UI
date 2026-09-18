import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { SidebarComponent } from './layout/sidebar.component';
import { TopbarComponent } from './layout/topbar.component';
import { LoaderComponent } from './shared/loader.component';
import { DataService } from './core/services/data.service';

@Component({
  selector:'app-root', standalone:true, imports:[RouterOutlet, SidebarComponent, TopbarComponent, LoaderComponent],
  template:`

   @if (ds.loader()) {
    <loader></loader>
  }
  <div class="app">
    <cc-sidebar></cc-sidebar>
    <div class="main">
      <cc-topbar></cc-topbar>
      <div class="content-wrap side-bar-scroll"><router-outlet></router-outlet></div>
    </div>
  </div>`,
  styles:[]
})

export class AppComponent { ds = inject(DataService); }
