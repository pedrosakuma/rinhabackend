CREATE TABLE public.pessoas (
 id UUID PRIMARY KEY NOT NULL,
 apelido VARCHAR(50) UNIQUE NOT NULL,
 nome VARCHAR(300) NOT NULL,
 nascimento DATE NOT NULL,
 stack TEXT[] NOT NULL
);