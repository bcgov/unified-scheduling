export interface SaveDayRecordItem {
  /** Null for new records; set to the existing record ID for updates. */
  id?: number | null;
  subCategoryMetricId: number;
  value: number;
  /** @nullable */
  comment?: string | null;
}

export interface SaveDayRequest {
  date: string;
  locationId: number;
  userId: string;
  status: string;
  records: SaveDayRecordItem[];
}
