# Tarea 1: Implementación del flujo de compras y gestión de inventario en ASP.NET Core MVC

Este proyecto consiste en el desarrollo de un módulo completo de compras y transacciones utilizando **ASP.NET Core MVC**, **Entity Framework Core**, e **Identity**, respaldado por una base de datos **PostgreSQL** (Northwind) bajo el enfoque Database First.

## Requisitos Previos

* **.NET SDK 10.0** o superior.
* Servidor de base de datos **PostgreSQL** activo con la base de datos 
orthwind restaurada (utilizando los scripts DDL y de datos).
* Herramientas de línea de comandos de EF Core (dotnet-ef).

## Configuración y Ejecución

### 1. Conexión de Base de Datos
La cadena de conexión debe ser configurada en el archivo ppsettings.json apuntando a tu servidor PostgreSQL local:

`json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=northwind;Username=postgres;Password=tu_contrasena"
}
`
*(Nota: No incluyas contrasenas reales en repositorios publicos).*

### 2. Generacion de Modelos (Scaffolding)
Para mapear la estructura, se ejecuto el siguiente comando de EF Core:
`ash
dotnet ef dbcontext scaffold "Host=localhost;Port=5432;Database=northwind;Username=postgres;Password=***" Npgsql.EntityFrameworkCore.PostgreSQL -o Models -c NorthwindContext --context-dir Data --force
`

### 3. Migraciones de Identity
Para activar el sistema de usuarios y roles, asegurate de aplicar las migraciones de seguridad:
`ash
dotnet ef migrations add AddIdentity --context ApplicationDbContext
dotnet ef database update --context ApplicationDbContext
`

### 4. Ejecucion del Proyecto
Para correr la aplicacion en entorno de desarrollo:
`ash
dotnet run
`
La aplicacion estara disponible tipicamente en http://localhost:5050.

## Funcionalidades Principales

* **Autenticacion y Roles:** Acceso protegido mediante ASP.NET Core Identity. Existen dos roles operativos: Admin y Customer.
* **Catalogo y Carrito:** Los clientes pueden visualizar productos con stock disponible, agregarlos a un carrito temporal (guardado en Session) y calcular automaticamente los subtotales y el gran total.
* **Transacciones Atomicas:** La confirmacion de la compra descuenta el inventario de la tabla Products, y registra la orden en Orders y OrderDetails en un solo bloque transaccional (BeginTransactionAsync), revirtiendo todo si ocurre un error.
* **Validaciones de Integridad:** Bloqueo de cantidades negativas o en cero, denegacion de compras sin stock suficiente y validaciones estrictas del modelo.
* **Panel Administrativo:** Permite a los usuarios con rol de Admin revisar todas las ordenes globales, monitorear el inventario (productos bajos en stock, agotados o descontinuados) y ajustar manualmente las unidades disponibles.

## Arquitectura y Tecnologias
* **Backend:** ASP.NET Core MVC (C#).
* **Frontend:** Razor Pages (CSHTML), Bootstrap, HTML5, CSS3.
* **ORM:** Entity Framework Core (Database First).
* **Seguridad:** Identity con Autorizacion por Roles.
* **Gestion de Estado:** Variables de Sesion (Session Memory Cache).

---
**Asignatura:** Desarrollo web para la integracion de la tecnologia  
**Estudiante:** Laglaguano Villavicencio Andy Joel  
**Periodo:** Octubre 2025 - Febrero 2026
