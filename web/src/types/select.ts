export type SelectCode = string | number;

export type SelectOption = {
  code: SelectCode;
  description: string;
};

export type SelectValue = SelectCode | SelectCode[] | null;
