import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal, viewChild } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatPaginator, MatPaginatorIntl, MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { Subject, combineLatest, of } from 'rxjs';
import { catchError, distinctUntilChanged, map, startWith, switchMap, tap } from 'rxjs/operators';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AggregationsDto, RequestQuery, RequestSortField, RequestStatus, ServiceRequestDto } from '../../core/models/request.models';
import { statusLabel } from '../../core/utils/display.utils';
import { EmptyStateComponent } from '../../shared/ui-state/empty-state.component';
import { ErrorStateComponent } from '../../shared/ui-state/error-state.component';
import { AggregationsCardsComponent } from './aggregations-cards.component';
import { FilterValue, RequestFiltersComponent } from './request-filters.component';
import { StatusUpdateDialogComponent } from './status-update-dialog.component';
import { errorInterceptor } from '../../core/interceptors/error.interceptor';
import { HebrewPaginatorIntl } from '../../core/i18n/hebrew-paginator-intl';
import { RequestsService } from '../../core/services/requests.service';
 
type SortDirection = 'asc' | 'desc' | '';

// Maps the table column ids to backend sort fields.
const SORT_FIELD_MAP: Record<string, RequestSortField> = {
  title: 'title',
  priority: 'priority',
  createdAt: 'createdAt',
  updatedAt: 'updatedAt',
};

@Component({
  selector: 'app-request-list',
  standalone: true,
  imports: [
    DatePipe,
    RouterLink,
    MatTableModule,
    MatSortModule,
    MatPaginatorModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatIconModule,
    MatButtonModule,
    MatProgressBarModule,
    MatTooltipModule,
    RequestFiltersComponent,
    AggregationsCardsComponent,
    EmptyStateComponent,
    ErrorStateComponent,
  ],
  providers: [{ provide: MatPaginatorIntl, useClass: HebrewPaginatorIntl }],
  template: `
    <h1 class="page-title">פניות</h1>

    <app-aggregations-cards [data]="aggregations()" [loading]="aggregationsLoading()" />

    <app-request-filters (filterChanged)="onFilterChanged($event)" />

    @if (loading()) {
      <mat-progress-bar mode="indeterminate" class="loader" />
    }

    @if (error()) {
      <app-error-state
        [message]="error()!"
        (retry)="retry()"
      />
    } @else if (!loading() && !items().length) {
      <app-empty-state
        icon="inbox"
        title="לא נמצאו פניות"
        message="לא נמצאו פניות המתאימות לסינון שהוגדר. נסו לשנות את תנאי החיפוש."
        actionLabel="ניקוי סינון"
        (actionTriggered)="reload()"
      />
    } @else {
      <div class="table-wrap">
        <table mat-table [dataSource]="items()" matSort (matSortChange)="onSortChanged($event)" class="full-width">
          <ng-container matColumnDef="title">
            <th mat-header-cell *matHeaderCellDef mat-sort-header="title">כותרת</th>
            <td mat-cell *matCellDef="let row">
              <span class="cell-title">{{ row.title }}</span>
            </td>
          </ng-container>

          <ng-container matColumnDef="organizationName">
            <th mat-header-cell *matHeaderCellDef>ארגון</th>
            <td mat-cell *matCellDef="let row">{{ row.organizationName }}</td>
          </ng-container>

          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef>סטטוס</th>
            <td mat-cell *matCellDef="let row">
              <span class="badge" [class]="'badge-' + row.status">{{ labelFor(row.status) }}</span>
            </td>
          </ng-container>

          <ng-container matColumnDef="priority">
            <th mat-header-cell *matHeaderCellDef mat-sort-header="priority">עדיפות</th>
            <td mat-cell *matCellDef="let row">
              <span class="prio" [class]="'prio-' + row.priority">{{ row.priority }}</span>
            </td>
          </ng-container>

          <ng-container matColumnDef="assignedTo">
            <th mat-header-cell *matHeaderCellDef>מטפל</th>
            <td mat-cell *matCellDef="let row">{{ row.assignedTo || '—' }}</td>
          </ng-container>

          <ng-container matColumnDef="createdAt">
            <th mat-header-cell *matHeaderCellDef mat-sort-header="createdAt">נוצר</th>
            <td mat-cell *matCellDef="let row">{{ row.createdAt | date: 'dd/MM/yyyy HH:mm' }}</td>
          </ng-container>

          <ng-container matColumnDef="updatedAt">
            <th mat-header-cell *matHeaderCellDef mat-sort-header="updatedAt">עודכן</th>
            <td mat-cell *matCellDef="let row">{{ row.updatedAt | date: 'dd/MM/yyyy HH:mm' }}</td>
          </ng-container>

          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef>פעולות</th>
            <td mat-cell *matCellDef="let row">
              <button
                mat-icon-button
                color="primary"
                matTooltip="עדכון סטטוס"
                (click)="openStatusDialog(row)"
              >
                <mat-icon>edit_note</mat-icon>
              </button>
              <a
                mat-icon-button
                matTooltip="היסטוריית שינויים"
                [routerLink]="['/requests', row.id, 'history']"
              >
                <mat-icon>history</mat-icon>
              </a>
            </td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="columns"></tr>
          <tr mat-row *matRowDef="let row; columns: columns"></tr>
        </table>
      </div>

      <mat-paginator
        [length]="totalCount()"
        [pageSizeOptions]="[10, 20, 50, 100]"
        [pageSize]="pageSize()"
        [pageIndex]="page() - 1"
        showFirstLastButtons
        (page)="onPageChanged($event)"
      />
    }
  `,
  styles: [
    `
      .page-title { margin: 0 0 20px; }
      .loader { margin-bottom: 12px; }
      .table-wrap { overflow-x: auto; background: #fff; border-radius: 8px; }
      .full-width { width: 100%; }
      .cell-title { font-weight: 500; }
      .badge {
        display: inline-block;
        padding: 2px 10px;
        border-radius: 12px;
        font-size: 0.8rem;
      }
      .badge-New { background: #e3f2fd; color: #0d47a1; }
      .badge-InProgress { background: #fff8e1; color: #8d6e00; }
      .badge-Waiting { background: #f3e5f5; color: #6a1b9a; }
      .badge-Completed { background: #e8f5e9; color: #1b5e20; }
      .prio { font-weight: 600; }
      .prio-High { color: #c62828; }
      .prio-Medium { color: #ef6c00; }
      .prio-Low { color: #2e7d32; }
    `,
  ],
})

export class RequestListComponent {
  private readonly service = inject(RequestsService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  readonly columns = [
    'title',
    'organizationName',
    'status',
    'priority',
    'assignedTo',
    'createdAt',
    'updatedAt',
    'actions',
  ];

  // UI state signals.
  readonly items = signal<ServiceRequestDto[]>([]);
  readonly totalCount = signal(0);
  readonly page = signal(1);
  readonly pageSize = signal(20);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly aggregations = signal<AggregationsDto | null>(null);
  readonly aggregationsLoading = signal(false);

  // Filter / sort state. These feed the query stream.
  private filters: FilterValue | null = null;
  private sortBy: RequestSortField | undefined;
  private sortDir: 'asc' | 'desc' | undefined;

  private readonly filter$ = new Subject<void>();
  private readonly refresh$ = new Subject<void>();

  // List stream: filters + paging + sort -> one HTTP call per change.
  // switchMap cancels an in-flight request whenever a newer filter arrives,
  // which is required when the user types fast (debounced in the filter child).
  private readonly list$ = combineLatest([
    this.filter$.pipe(startWith(void 0)),
    this.refresh$.pipe(startWith(void 0)),
  ])
    .pipe(
      map(() => this.buildQuery()),
      distinctUntilChanged((a, b) => JSON.stringify(a) === JSON.stringify(b)),
      tap(() => {
        this.loading.set(true);
        this.error.set(null);
      }),
      switchMap((query) =>
        this.service.getRequests(query).pipe(
          catchError((err: HttpErrorResponse) => {
            this.error.set(
              err.status === 0
                ? 'לא ניתן להתחבר לשרת. בדקו את החיבור ונסו שוב.'
                : 'אירעה שגיאה בטעינת הפניות.',
            );
            return of(null);
          }),
          tap((result:any) => {
            this.loading.set(false);
            if (result) {
              this.items.set(result.items);
              this.totalCount.set(result.totalCount);
            }
          }),
        ),
      ),
      takeUntilDestroyed(),
    );

  constructor() {
    // Start the list stream.
    this.list$.subscribe();
    this.loadAggregations();
  }

  onFilterChanged(value: FilterValue): void {
    this.filters = value;
    this.page.set(1); // any filter change resets to the first page
    this.filter$.next();
  }

  onSortChanged(sort: Sort): void {
    this.sortBy = sort.direction ? SORT_FIELD_MAP[sort.active] : undefined;
    this.sortDir = sort.direction || undefined;
    this.page.set(1);
    this.refresh$.next();
  }

  onPageChanged(event: PageEvent): void {
    this.pageSize.set(event.pageSize);
    this.page.set(event.pageIndex + 1);
    this.refresh$.next();
  }

  reload(): void {
    this.refresh$.next();
  }

  retry(): void {
    this.refresh$.next();
    this.loadAggregations();
  }

  labelFor(status: RequestStatus): string {
    return statusLabel(status);
  }

  openStatusDialog(row: ServiceRequestDto): void {
    this.dialog
      .open(StatusUpdateDialogComponent, {
        data: { request: row },
        width: '420px',
        direction: 'rtl',
      })
      .afterClosed()
      .subscribe((newStatus: RequestStatus | undefined) => {
        if (!newStatus) return;

        this.service.updateStatus(row.id, { status: newStatus, rowVersion: row.rowVersion }).subscribe({
          next: () => {
            this.snackBar.open('הסטטוס עודכן בהצלחה.', 'סגירה', { duration: 3500 });
            // Aggregations are cached server-side; refresh both the row and the cards.
            this.reload();
            this.loadAggregations();
          },
          error: (err: HttpErrorResponse) => {
            if (err.status === 409) {
              // Optimistic concurrency conflict: someone else updated first.
              this.snackBar.open(
                'הפנייה עודכנה על ידי משתמש אחר. הנתונים רועננו — בדקו ונסו שוב.',
                'סגירה',
                { duration: 6000 },
              );
              this.reload();
              this.loadAggregations();
            } else if (err.status === 400) {
              this.snackBar.open('השינוי המבוקש אינו חוקי עבור פנייה זו.', 'סגירה', {
                duration: 5000,
              });
            } else if (err.status === 404) {
              this.snackBar.open('הפנייה לא נמצאה.', 'סגירה', { duration: 5000 });
              this.reload();
            }
            // Other statuses are already surfaced by the global interceptor.
          },
        });
      });
  }

  private loadAggregations(): void {
    this.aggregationsLoading.set(true);
    this.service
      .getAggregations()
      .pipe(
        catchError(() => of(null)),
        tap((data) => {
          this.aggregationsLoading.set(false);
          if (data) this.aggregations.set(data as any);
        }),
        takeUntilDestroyed(),
      )
      .subscribe();
  }

  private buildQuery(): RequestQuery {
    const f = this.filters;
    return {
      page: this.page(),
      pageSize: this.pageSize(),
      search: f?.search || undefined,
      status: f?.statuses?.length ? f.statuses : undefined,
      priority: f?.priority ?? undefined,
      organizationName: f?.organizationName || undefined,
      assignedTo: f?.assignedTo || undefined,
      fromDate: f?.fromDate ? this.formatDate(f.fromDate) : undefined,
      toDate: f?.toDate ? this.formatDate(f.toDate) : undefined,
      sortBy: this.sortBy,
      sortDir: this.sortDir,
    };
  }

  private formatDate(d: Date): string {
    const y = d.getFullYear();
    const m = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${y}-${m}-${day}`;
  }
}

// Hebrew labels for the paginator (RTL).
