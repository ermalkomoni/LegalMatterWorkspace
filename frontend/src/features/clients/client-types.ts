export type ClientInput = {
  name: string;
  email: string;
  phone: string | null;
  postalAddress: string | null;
};

export type Client = ClientInput & { id: number };
export type ClientChoice = Pick<Client, "id" | "name">;
