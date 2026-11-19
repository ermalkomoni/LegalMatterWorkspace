import { useId, useState, type ChangeEvent, type FormEvent } from "react";
import { Field, fieldDescription } from "@/components/workspace-ui";
import type { FieldErrors } from "@/lib/api";

type FieldName = "title" | "description" | "dueDate" | "date" | "text";
type ItemValues = Partial<Record<FieldName, string | null>>;

export type ItemField = {
  name: FieldName;
  label: string;
  type: "text" | "date" | "textarea";
  required?: boolean;
  maxLength?: number;
};

export function MatterItemForm({ fields, item, itemName, saving, errors, onSave, onCancel }: {
  fields: readonly ItemField[];
  item?: ItemValues;
  itemName: string;
  saving: boolean;
  errors?: FieldErrors;
  onSave: (values: ItemValues) => void;
  onCancel: () => void;
}) {
  const formId = useId();
  const [draft, setDraft] = useState<ItemValues>(item ?? {});
  const [validation, setValidation] = useState<FieldErrors>({});

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const values: ItemValues = {};
    const nextErrors: FieldErrors = {};
    for (const field of fields) {
      const value = draft[field.name]?.trim();
      values[field.name] = value || null;
      if (field.required && !value) nextErrors[field.name.toLowerCase()] = [`Enter a ${field.label.toLowerCase()}.`];
    }
    setValidation(nextErrors);
    if (Object.keys(nextErrors).length === 0) onSave(values);
  }

  return (
    <form method="post" onSubmit={submit} className="rounded-lg bg-stone-50 p-4 sm:p-5">
      <fieldset disabled={saving} className="min-w-0 space-y-4">
        <legend className="mb-3 font-semibold">{item ? `Edit ${itemName}` : `New ${itemName}`}</legend>
        {fields.map(({ name, label, type, required, maxLength }, index) => {
          const id = `${formId}-${name}`;
          const key = name.toLowerCase();
          const error = validation[key] ? validation[key][0] : errors?.[key]?.[0];
          const props = {
            id, required, maxLength,
            className: type === "date" ? "admin-input max-w-60" : "admin-input",
            value: draft[name] ?? "",
            autoFocus: index === 0,
            onChange: (event: ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => {
              const value = event.target.value;
              setDraft((current) => ({ ...current, [name]: value }));
              setValidation((current) => ({ ...current, [key]: [] }));
            },
            ...fieldDescription(id, error),
          };
          return (
            <Field key={name} id={id} label={label} error={error}>
              {type === "textarea" ? <textarea {...props} rows={4} /> : (
                <input {...props} type={type}
                  min={type === "date" ? "0001-01-01" : undefined}
                  max={type === "date" ? "9999-12-31" : undefined} />
              )}
            </Field>
          );
        })}
        <div className="flex flex-wrap gap-2">
          <button className="admin-primary" type="submit">
            {saving ? "Saving…" : item ? "Save changes" : `Add ${itemName}`}
          </button>
          <button className="admin-secondary" type="button" onClick={onCancel}>Cancel</button>
        </div>
      </fieldset>
    </form>
  );
}
