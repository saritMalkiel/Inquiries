import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient, HttpParams } from '@angular/common/http';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { Subject, switchMap, tap, catchError, of, startWith, debounceTime } from 'rxjs';
import { toSignal } from '@angular/core/rxjs-interop';

interface Item { id: number; title: string; organizationName: string; status: string;
                  priority: string; assignedTo: string | null; createdAt: string; }
interface Paged { page: number; pageSize: number; total: number; items: Item[]; }

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <h2>פניות</h2>

    <input [formControl]="search" placeholder="חיפוש" />
    <select [formControl]="status">
      <option value="">הכל</option>
      <option>New</option><option>InProgress</option>
      <option>Waiting</option><option>Completed</option>
    </select>

    <p *ngIf="loading()">טוען...</p>
    <p *ngIf="error()" style="color:red">{{ error() }}</p>

    <table *ngIf="data() as d" border="1" style="width:100%;border-collapse:collapse">
      <tr><th>#</th><th>כותרת</th><th>ארגון</th><th>סטטוס</th><th>עדיפות</th><th>נוצר</th></tr>
      <tr *ngFor="let x of d.items">
        <td>{{ x.id }}</td><td>{{ x.title }}</td><td>{{ x.organizationName }}</td>
        <td>{{ x.status }}</td><td>{{ x.priority }}</td>
        <td>{{ x.createdAt | date:'dd/MM/yy' }}</td>
      </tr>
    </table>

    <div *ngIf="data() as d">
      עמוד {{ d.page }} מתוך {{ pages(d) }} ({{ d.total }} רשומות)
      <button (click)="move(-1, d)">הקודם</button>
      <button (click)="move(1, d)">הבא</button>
    </div>
  `
})
export class AppComponent {
  private http = inject(HttpClient);

  search = new FormControl('');
  status = new FormControl('');

  loading = signal(false);
  error = signal<string | null>(null);

  page = 1;
  private reload = new Subject<void>();

  data = toSignal(
    this.reload.pipe(
      startWith(void 0),
      switchMap(() => {
        this.loading.set(true);
        this.error.set(null);
        let p = new HttpParams().set('page', this.page).set('pageSize', 20);
        if (this.search.value) p = p.set('search', this.search.value);
        if (this.status.value) p = p.set('status', this.status.value);
        return this.http.get<Paged>('/api/requests', { params: p }).pipe(
          tap(() => this.loading.set(false)),
          catchError(() => { this.loading.set(false); this.error.set('שגיאה'); return of(null as any); })
        );
      })
    ),
    { initialValue: null }
  );

  constructor() {
    this.search.valueChanges.pipe(debounceTime(300)).subscribe(() => { this.page = 1; this.reload.next(); });
    this.status.valueChanges.subscribe(() => { this.page = 1; this.reload.next(); });
  }

  pages(d: Paged) { return Math.max(1, Math.ceil(d.total / d.pageSize)); }

  move(dir: number, d: Paged) {
    const next = this.page + dir;
    if (next < 1 || next > this.pages(d)) return;
    this.page = next;
    this.reload.next();
  }
}
