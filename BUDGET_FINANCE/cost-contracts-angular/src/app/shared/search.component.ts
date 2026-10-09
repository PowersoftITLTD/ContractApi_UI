

// import {
//   Component, ElementRef, EventEmitter, HostListener, Output,
//   ViewChild, signal, OnDestroy
// } from '@angular/core';
// import { CommonModule } from '@angular/common';
// import { FormsModule } from '@angular/forms';
// import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';

// @Component({
//   selector: 'cc-search',
//   standalone: true,
//   imports: [CommonModule, FormsModule],
//   template: `
//     <button type="button"
//             class="search-icon"
//             [class.active]="isOpen()"
//             aria-label="Search (Ctrl+K)"
//             [attr.aria-expanded]="isOpen()"
//             aria-controls="searchPanel"
//             (click)="toggle()">
//       <svg viewBox="0 0 24 24" width="16" height="16" aria-hidden="true">
//         <circle cx="11" cy="11" r="7" fill="none" stroke="currentColor" stroke-width="2"/>
//         <line x1="16.5" y1="16.5" x2="21" y2="21"
//               stroke="currentColor" stroke-width="2" stroke-linecap="round"/>
//       </svg>
//     </button>

//     <!-- Panel is fixed to the viewport, centered below the topbar -->
//     <div id="searchPanel"
//          class="search-panel"
//          [class.open]="isOpen()"
//          role="dialog"
//          aria-label="Search">
//       <input #input
//              type="text"
//              class="search-input"
//              placeholder="Search projects, entities, metrics…  (Esc to close)"
//              autocomplete="off"
//              [(ngModel)]="query"
//              (ngModelChange)="onInput($event)"
//              (keydown.escape)="closeAndBlur()" />
//     </div>
//   `,
//   styles: [`
//     :host { display: inline-block; }

//     .search-icon {
//       display: inline-flex;
//       align-items: center;
//       justify-content: center;
//       width: 34px;
//       height: 34px;
//       border: 1px solid var(--line);
//       border-radius: 8px;
//       background: #fff;
//       color: var(--t2);
//       cursor: pointer;
//       transition: background .15s ease, color .15s ease, border-color .15s ease;
//     }
//     .search-icon:hover,
//     .search-icon:focus-visible,
//     .search-icon.active {
//       background: var(--bg);
//       color: var(--navy);
//       border-color: var(--cyan-line);
//       outline: none;
//     }

//     .search-panel {
//       position: fixed;
//       top: 62px;
//       left: 50%;
//       transform: translate(-50%, -6px) scale(.98);
//       transform-origin: top center;

//       width: min(720px, calc(100vw - 32px));

//       background: #fff;
//       border: 1px solid var(--line);
//       border-radius: 12px;
//       box-shadow: 0 12px 32px rgba(20, 30, 60, .14);

//       padding: 10px 12px;
//       z-index: 50;

//       opacity: 0;
//       visibility: hidden;
//       pointer-events: none;
//       transition: opacity .18s ease,
//                   transform .18s cubic-bezier(.2, .7, .2, 1),
//                   visibility 0s linear .18s;
//     }
//     .search-panel.open {
//       opacity: 1;
//       visibility: visible;
//       pointer-events: auto;
//       transform: translate(-50%, 0) scale(1);
//       transition: opacity .18s ease,
//                   transform .18s cubic-bezier(.2, .7, .2, 1),
//                   visibility 0s;
//     }

//     .search-input {
//       width: 100%;
//       box-sizing: border-box;
//       font: 400;
//       font-size: 13px;
//       padding: 9px 12px;
//       border: 1px solid var(--line);
//       border-radius: 8px;
//       background: var(--bg);
//       color: var(--t1);
//       outline: none;
//       transition: border-color .15s ease, background .15s ease;
//     }

//     .search-input::placeholder { color: var(--t3); }
//     .search-input:focus {
//       background: #fff;
//       border-color: var(--cyan-line);
//     }

//     @media (max-width: 640px) {
//       .search-panel { top: 58px; }
//     }
//   `]
// })
// export class SearchComponent implements OnDestroy {
//   @Output() search = new EventEmitter<string>();

//   @ViewChild('input') inputRef?: ElementRef<HTMLInputElement>;

//   isOpen = signal(false);
//   query = '';

//   private readonly input$ = new Subject<string>();
//   private readonly sub = this.input$
//     .pipe(debounceTime(250), distinctUntilChanged())
//     .subscribe(q => this.search.emit(q));

//   open() {
//     if (!this.isOpen()) this.isOpen.set(true);
//     // Focus the input on the next tick so it's visible
//     setTimeout(() => this.inputRef?.nativeElement.focus(), 0);
//   }

//   toggle() {
//     if (this.isOpen()) {
//       this.closeAndBlur();
//     } else {
//       this.open();
//     }
//   }

//   close() {
//     this.isOpen.set(false);
//   }

//   closeAndBlur() {
//     this.close();
//     this.inputRef?.nativeElement.blur();
//   }

//   onInput(value: string) { this.input$.next(value); }

//   /** Ctrl+K (or ⌘+K on Mac) opens search from anywhere */
//   @HostListener('document:keydown', ['$event'])
//   onGlobalKeydown(event: KeyboardEvent) {
//     const isK = event.key === 'k' || event.key === 'K';
//     const modifier = event.ctrlKey || event.metaKey;

//     if (isK && modifier) {
//       event.preventDefault();   // stop browser's default (e.g. focus address bar)
//       this.open();
//     }
//   }

//   @HostListener('document:keydown.escape')
//   onEscape() {
//     if (this.isOpen()) this.closeAndBlur();
//   }

//   ngOnDestroy() { this.sub.unsubscribe(); }
// }


import {
  Component, ElementRef, EventEmitter, HostListener, Output,
  ViewChild, OnDestroy
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';

@Component({
  selector: 'cc-search',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="search-bar">
      <svg class="search-bar__icon" viewBox="0 0 24 24" width="16" height="16" aria-hidden="true">
        <circle cx="11" cy="11" r="7" fill="none" stroke="currentColor" stroke-width="2"/>
        <line x1="16.5" y1="16.5" x2="21" y2="21"
              stroke="currentColor" stroke-width="2" stroke-linecap="round"/>
      </svg>

      <input #input
             type="text"
             class="search-bar__input"
             placeholder="Search projects, entities, metrics…"
             autocomplete="off"
             aria-label="Search (Ctrl+K)"
             [(ngModel)]="query"
             (ngModelChange)="onInput($event)"
             (keydown.escape)="clearAndBlur()" />

      <kbd class="search-bar__kbd" aria-hidden="true">Ctrl K</kbd>
    </div>
  `,
  styles: [`
    :host { display: block; width: 100%; }

    .search-bar {
      display: flex;
      align-items: center;
      gap: 8px;
      width: 50%;
      box-sizing: border-box;
      padding: 0 12px;
      border: 1px solid var(--line);
      border-radius: 8px;
      background: var(--bg);
      transition: border-color .15s ease, background .15s ease;
    }

    .search-bar:focus-within {
      background: #fff;
      border-color: var(--cyan-line);
    }

    .search-bar__icon {
      flex: 0 0 auto;
      color: var(--t3);
    }

    .search-bar__input {
      flex: 1 1 auto;
      min-width: 0;
      font-size: 13px;
      padding: 9px 0;
      border: none;
      background: transparent;
      color: var(--t1);
      outline: none;
    }

    .search-bar__input::placeholder { color: var(--t3); }

    .search-bar__kbd {
      flex: 0 0 auto;
      font-family: inherit;
      font-size: 11px;
      line-height: 1;
      color: var(--t3);
      background: #fff;
      border: 1px solid var(--line);
      border-radius: 6px;
      padding: 4px 6px;
      white-space: nowrap;
    }
  `]
})
export class SearchComponent implements OnDestroy {
  @Output() search = new EventEmitter<string>();

  @ViewChild('input') inputRef?: ElementRef<HTMLInputElement>;

  query = '';

  private readonly input$ = new Subject<string>();
  private readonly sub = this.input$
    .pipe(debounceTime(250), distinctUntilChanged())
    .subscribe(q => this.search.emit(q));

  /** Ctrl+K (or ⌘+K on Mac) focuses the search bar from anywhere */
  @HostListener('document:keydown', ['$event'])
  onGlobalKeydown(event: KeyboardEvent) {
    const isK = event.key === 'k' || event.key === 'K';
    const modifier = event.ctrlKey || event.metaKey;
    if (isK && modifier) {
      event.preventDefault(); // stop the browser's address-bar shortcut
      this.focus();
    }
  }

  focus() {
    const el = this.inputRef?.nativeElement;
    if (!el) return;
    el.focus();
    el.select();
  }

  onInput(value: string) { this.input$.next(value); }

  clearAndBlur() {
    this.query = '';
    this.input$.next('');
    this.inputRef?.nativeElement.blur();
  }

  ngOnDestroy() { this.sub.unsubscribe(); }
}