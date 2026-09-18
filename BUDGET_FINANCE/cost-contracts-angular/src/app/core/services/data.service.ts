import { HttpClient } from "@angular/common/http";
import { Injectable, signal, computed } from "@angular/core";
import { finalize } from "rxjs/operators"; 
import { APP_CONFIG } from "../config";
import { DashboardPayload, Scope, ProjectData } from "../models/models";

@Injectable({ providedIn:'root' })
export class DataService {
  private _payload = signal<DashboardPayload | null>(null);
  projectId = signal<string>('FWG');
  scope     = signal<Scope>('entity');
  loader    = signal<boolean>(false); // loader state
  level = signal<'summary' | 'group' | 'code' | 'wopo_details' | 'wopojv'>('summary');
  
  payload  = computed(() => this._payload());
  projects = computed(() => this._payload()?.projects ?? []);
  entity   = computed(() => this._payload()?.entity ?? null);
  
  current  = computed<ProjectData | null>(() => {
    const p = this._payload();
    if (!p) return null;
    return p.data?.[this.projectId()] || null;
  });

    projectCount  = computed<any | null>(() => {
    const p = this._payload();
    if (!p) return null;
    return p.data?.[this.projectId()] || null;
  });

  constructor(private http: HttpClient) {
    const url = !APP_CONFIG.useApi ? `${APP_CONFIG.apiBase2}/ContractBudget/ContractBudget` : APP_CONFIG.dataUrl;

    // 1. Turn on the loader immediately before the HTTP request starts
    this.loader.set(true);
    
    this.http.get<{ status: string; message: string; data: DashboardPayload }>(url)
      .pipe(
        // 2. Automatically sets loader to false when the stream completes or throws an error
        finalize(() => this.loader.set(false)) 
      )
      .subscribe({
        next: (response) => {
          const payload = response?.data;
          this._payload.set(payload);

          // console.log('payload: ', payload)
        },
        error: (err) => {
          console.error('Error fetching data:', err);
        }
      });
  }
  
  setProject(id: string) { 
    this.projectId.set(id);
    
    this.level.set('summary');
  }
  
  setScope(s: Scope) { 
    this.scope.set(s); 
    this.level.set('summary');             
  }
  
  entitySum(field: keyof ProjectData['totals']): number {
    const p = this._payload(); 
    if (!p) return 0;
    
    return Object.values(p.data).reduce((acc, d: any) => {
      const totals = d?.totals || d?.data?.totals || {};
      return acc + (Number(totals[field]) || 0);
    }, 0);
  }
}
