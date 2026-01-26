export type KeyCloakUser = {
    id: string;
    name: string;
    email: string;
    emailVerified: boolean;
    createdAt: Date;
    updatedAt: Date;
    roles: string ;
    image?: string | null | undefined;
};
