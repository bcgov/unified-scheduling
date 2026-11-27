export type SelectCode = string | number;

export type SelectOption = {
  code: SelectCode;
  description: string;
  props?: {
    disabled?: boolean;
  };
};

export type SelectValue = SelectCode | SelectCode[] | null;
