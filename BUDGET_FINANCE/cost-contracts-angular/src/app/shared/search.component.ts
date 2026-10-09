import {
  Component, ElementRef, EventEmitter, HostListener, Output,
  ViewChild, signal, OnDestroy
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';

@Component({
  selector: 'cc-search',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <button type="button"
            class="search-icon"
            [class.active]="isOpen()"
            aria-label="Search"
            [attr.aria-expanded]="isOpen()"
            aria-controls="searchPanel"
            (click)="toggle()"
            (mouseenter)="open()"
            (focus)="open()">
      <svg viewBox="0 0 24 24" width="16" height="16" aria-hidden="true">
        <circle cx="11" cy="11" r="7" fill="none" stroke="currentColor" stroke-width="2"/>
        <line x1="16.5" y1="16.5" x2="21" y2="21"
              stroke="currentColor" stroke-width="2" stroke-linecap="round"/>
      </svg>
    </button>

    <!-- Panel is fixed to the viewport, centered below the topbar -->
    <div id="searchPanel"
         class="search-panel"
         [class.open]="isOpen()"
         role="dialog"
         aria-label="Search"
         (mouseenter)="cancelClose()"
         (mouseleave)="onMouseLeave()">
      <input #input
             type="text"
             class="search-input"
             placeholder="Search projects, entities, metrics…"
             autocomplete="off"
             [(ngModel)]="query"
             (ngModelChange)="onInput($event)"
             (keydown.escape)="closeAndBlur()"
             (blur)="onInputBlur()" />
    </div>
  `,
  styles: [`
    :host { display: inline-block; }

    /* Same look as the previous icon/button */
    .search-icon {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 34px;
      height: 34px;
      border: 1px solid var(--line);
      border-radius: 8px;
      background: #fff;
      color: var(--t2);
      cursor: pointer;
      transition: background .15s ease, color .15s ease, border-color .15s ease;
    }
    .search-icon:hover,
    .search-icon:focus-visible,
    .search-icon.active {
      background: var(--bg);
      color: var(--navy);
      border-color: var(--cyan-line);
      outline: none;
    }

    /* Fixed to viewport, centered, just below the topbar */
    .search-panel {
      position: fixed;
      top: 62px;                    /* topbar height (11px pad *2 + ~34px content + 1px border) */
      left: 50%;
      transform: translate(-50%, -6px) scale(.98);
      transform-origin: top center;

      width: min(720px, calc(100vw - 32px));

      background: #fff;
      border: 1px solid var(--line);
      border-radius: 12px;
      box-shadow: 0 12px 32px rgba(20, 30, 60, .14);

      padding: 10px 12px;
      z-index: 50;

      opacity: 0;
      visibility: hidden;
      pointer-events: none;
      transition: opacity .18s ease,
                  transform .18s cubic-bezier(.2, .7, .2, 1),
                  visibility 0s linear .18s;
    }
    .search-panel.open {
      opacity: 1;
      visibility: visible;
      pointer-events: auto;
      transform: translate(-50%, 0) scale(1);
      transition: opacity .18s ease,
                  transform .18s cubic-bezier(.2, .7, .2, 1),
                  visibility 0s;
    }

    /* Preserves the original search field styling */
    .search-input {
      width: 100%;
      box-sizing: border-box;
      font: 400;
      font-size: 13px;
      padding: 9px 12px;
      border: 1px solid var(--line);
      border-radius: 8px;
      background: var(--bg);
      color: var(--t1);
      outline: none;
      transition: border-color .15s ease, background .15s ease;
    }
    
    .search-input::placeholder { color: var(--t3); }
    .search-input:focus {
      background: #fff;
      border-color: var(--cyan-line);
    }

    @media (max-width: 640px) {
      .search-panel { top: 58px; }
    }
  `]
})
export class SearchComponent implements OnDestroy {
  @Output() search = new EventEmitter<string>();

  @ViewChild('input') inputRef?: ElementRef<HTMLInputElement>;

  isOpen = signal(false);
  query = '';

  private closeTimer?: ReturnType<typeof setTimeout>;
  private readonly input$ = new Subject<string>();
  private readonly sub = this.input$
    .pipe(debounceTime(250), distinctUntilChanged())
    .subscribe(q => this.search.emit(q));

  open() {
  
    this.cancelClose();
    if (!this.isOpen()) this.isOpen.set(true);
  }

  toggle() {
    if (this.isOpen()) {
      this.close();
    } else {
      this.open();
      setTimeout(() => this.inputRef?.nativeElement.focus(), 0);
    }
  }

close() {
  if (!this.query) this.isOpen.set(false);
} 
  cancelClose() {
    if (this.closeTimer) { clearTimeout(this.closeTimer); this.closeTimer = undefined; }
  }

  /** Small grace period so moving cursor from icon toward panel doesn't flicker */
  onMouseLeave() {
    this.cancelClose();
    this.closeTimer = setTimeout(() => this.close(), 140);
  }

  onInput(value: string) { this.input$.next(value); }

  onInputBlur() {
    setTimeout(() => {
      if (!this.isOpen()) return;
      const root = this.inputRef?.nativeElement.closest('.search-panel') as HTMLElement | null;
      // Only close on blur if the pointer isn't over the panel (or its trigger)
      const triggerHovered = (document.querySelector('cc-search .search-icon') as HTMLElement)?.matches(':hover');
      if (root && !root.matches(':hover') && !triggerHovered) this.close();
    }, 100);
  }

  closeAndBlur() {
    this.close();
    this.inputRef?.nativeElement.blur();
  }

  @HostListener('document:keydown.escape')
  onEscape() { if (this.isOpen()) this.closeAndBlur(); }

  ngOnDestroy() { this.cancelClose(); this.sub.unsubscribe(); }
}