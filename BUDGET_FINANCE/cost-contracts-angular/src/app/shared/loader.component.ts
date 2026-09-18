import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';

@Component({
  selector: 'loader',
  standalone: true,
  imports: [CommonModule],
  template: `
     <div class="ul-loader-overlay">
    <div class="ul-loader-container">
      <div class="ul-loader"></div>
      <p class="ul-loader-text">Loading Details...</p>
    </div>
  </div>
  `,
  styles: ``
})
export class LoaderComponent {

}
