
using Dsw2025Tpi.Application.Dtos;
using Dsw2025Tpi.Application.Exceptions;
using Dsw2025Tpi.Application.Services;
using Dsw2025Tpi.Domain.Entities;
using Dsw2025Tpi.Domain.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Xunit;
using System.Collections.Generic;
using System.Linq;

namespace Dsw2025Tpi.UnitTests.Services;

public class ProductsManagementServiceTests
{
    [Fact]
    public async Task AddProduct_WithValidData_ReturnsProductAndCallsRepositoryAdd()
    {
        // ARRANGE: preparar los datos y las dependencias simuladas
        var request = new ProductModel.Request(
            Sku: "SKU-001",
            InternalCode: "INT-001",
            Name: "Mouse Gamer",
            Description: "Mouse de prueba",
            CurrentUnitPrice: 15000m,
            StockQuantity: 10
        );

        var repositoryMock = new Mock<IRepository>();

        repositoryMock
            .Setup(r => r.First<Product>(
                It.IsAny<Expression<Func<Product, bool>>>(),
                It.IsAny<string[]>()))
            .ReturnsAsync((Product?)null);

        repositoryMock
            .Setup(r => r.Add(It.IsAny<Product>()))
            .ReturnsAsync((Product product) => product);

        var service = new ProductsManagementService(
            repositoryMock.Object,
            NullLogger<ProductsManagementService>.Instance
        );

        // ACT: ejecutar el método real
        var result = await service.AddProduct(request);

        // ASSERT: verificar los datos devueltos
        Assert.NotNull(result);
        Assert.Equal(request.Sku, result.Sku);
        Assert.Equal(request.InternalCode, result.InternalCode);
        Assert.Equal(request.Name, result.Name);
        Assert.Equal(request.Description, result.Description);
        Assert.Equal(request.CurrentUnitPrice, result.CurrentUnitPrice);
        Assert.Equal(request.StockQuantity, result.StockQuantity);
        Assert.True(result.IsActive);

        // Verificar que el repositorio agrega el producto exactamente una vez
        repositoryMock.Verify(
            r => r.Add(It.Is<Product>(p =>
                p.Sku == request.Sku &&
                p.Name == request.Name &&
                p.CurrentUnitPrice == request.CurrentUnitPrice &&
                p.StockQuantity == request.StockQuantity
            )),
            Times.Once
        );
    }

    [Fact]
    public async Task AddProduct_WithDuplicateSku_ThrowsDuplicatedEntityException()
    {
        // ARRANGE: preparar un producto con SKU ya existente
        var request = new ProductModel.Request(
            Sku: "SKU-001",
            InternalCode: "INT-002",
            Name: "Mouse Gamer",
            Description: "Producto duplicado",
            CurrentUnitPrice: 15000m,
            StockQuantity: 10
        );

        var existingProduct = new Product(
            "SKU-001",
            "Mouse existente",
            12000m,
            5
        );

        var repositoryMock = new Mock<IRepository>();

        repositoryMock
            .Setup(r => r.First<Product>(
                It.IsAny<Expression<Func<Product, bool>>>(),
                It.IsAny<string[]>()))
            .ReturnsAsync(existingProduct);

        var service = new ProductsManagementService(
            repositoryMock.Object,
            NullLogger<ProductsManagementService>.Instance
        );

        // ACT: intentar crear un producto con SKU duplicado
        var exception = await Assert.ThrowsAsync<
            DuplicatedEntityException>(
            () => service.AddProduct(request)
        );

        // ASSERT: verificar la excepción
        Assert.Contains(request.Sku, exception.Message);

        // Comprobar que no se agregó ningún producto
        repositoryMock.Verify(
            r => r.Add(It.IsAny<Product>()),
            Times.Never
        );
    }

    [Theory]
    [InlineData(0, 15000, "La cantidad de stock debe ser mayor o igual a cero")]
    [InlineData(-5, 15000, "La cantidad de stock debe ser mayor o igual a cero")]
    [InlineData(10, -100, "El precio unitario ser mayores a cero")]
    public async Task AddProduct_WithInvalidStockOrPrice_ThrowsInvalidDataException(
        int stock,
        int price,
        string expectedMessage)
    {
        // ARRANGE: preparar una solicitud con valores inválidos
        var request = new ProductModel.Request(
            Sku: "SKU-INVALIDO",
            InternalCode: "INT-INVALIDO",
            Name: "Producto inválido",
            Description: "Producto para comprobar validaciones",
            CurrentUnitPrice: price,
            StockQuantity: stock
        );

        var repositoryMock = new Mock<IRepository>();

        var service = new ProductsManagementService(
            repositoryMock.Object,
            NullLogger<ProductsManagementService>.Instance
        );

        // ACT: ejecutar la creación y capturar la excepción
        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => service.AddProduct(request)
        );

        // ASSERT: verificar mensaje y ausencia de llamadas al repositorio
        Assert.Equal(expectedMessage, exception.Message);

        repositoryMock.Verify(
            r => r.First<Product>(
                It.IsAny<Expression<Func<Product, bool>>>(),
                It.IsAny<string[]>()),
            Times.Never
        );

        repositoryMock.Verify(
            r => r.Add(It.IsAny<Product>()),
            Times.Never
        );
    }


    [Fact]
    public async Task GetProducts_WhenIncludeInactiveIsFalse_ReturnsOnlyActiveProducts()
    {
        // ARRANGE: productos activos e inactivos
        var products = new List<Product>
    {
        new Product("SKU-001", "Mouse Gamer", 15000m, 10),
        new Product("SKU-002", "Teclado Mecánico", 25000m, 5),
        new Product("SKU-003", "Monitor", 100000m, 3)
        {
            IsActive = false
        }
    };

        var repositoryMock = new Mock<IRepository>();

        repositoryMock
            .Setup(r => r.GetFiltered<Product>(
                It.IsAny<Expression<Func<Product, bool>>>(),
                It.IsAny<string[]>()))
            .ReturnsAsync((
                Expression<Func<Product, bool>> predicate,
                string[] include) =>
                products.Where(predicate.Compile()).ToList());

        var service = new ProductsManagementService(
            repositoryMock.Object,
            NullLogger<ProductsManagementService>.Instance
        );

        var filter = new ProductModel.FilterProduct(
            Status: null,
            Search: null,
            PageNumber: 1,
            PageSize: 10
        );

        // ACT: consultar sin incluir productos inactivos
        var result = await service.GetProducts(
            filter,
            includeInactive: false
        );

        // ASSERT: comprobar que sólo devuelve activos
        Assert.Equal(2, result.ProductItems.Count);
        Assert.Equal(2, result.Total);
        Assert.All(result.ProductItems, p => Assert.True(p.IsActive));

        repositoryMock.Verify(
            r => r.GetFiltered<Product>(
                It.IsAny<Expression<Func<Product, bool>>>(),
                It.IsAny<string[]>()),
            Times.Once
        );
    }


    [Fact]
    public async Task GetProducts_WithPaginationFilter_AppliesSkipAndTakeCorrectly()
    {
        // ARRANGE: preparar 15 productos activos
        var products = Enumerable.Range(1, 15)
            .Select(i => new Product(
                $"SKU-{i:00}",
                $"Producto {i:00}",
                1000m + i,
                10
            ))
            .ToList();

        var repositoryMock = new Mock<IRepository>();

        repositoryMock
            .Setup(r => r.GetFiltered<Product>(
                It.IsAny<Expression<Func<Product, bool>>>(),
                It.IsAny<string[]>()))
            .ReturnsAsync((
                Expression<Func<Product, bool>> predicate,
                string[] include) =>
                products.Where(predicate.Compile()).ToList());

        var service = new ProductsManagementService(
            repositoryMock.Object,
            NullLogger<ProductsManagementService>.Instance
        );

        var filter = new ProductModel.FilterProduct(
            Status: null,
            Search: null,
            PageNumber: 2,
            PageSize: 5
        );

        // ACT
        var result = await service.GetProducts(filter);

        // ASSERT
        Assert.Equal(15, result.Total);
        Assert.Equal(5, result.ProductItems.Count);

        Assert.Equal(
            new[]
            {
            "Producto 06",
            "Producto 07",
            "Producto 08",
            "Producto 09",
            "Producto 10"
            },
            result.ProductItems.Select(p => p.Name).ToArray()
        );

        repositoryMock.Verify(
            r => r.GetFiltered<Product>(
                It.IsAny<Expression<Func<Product, bool>>>(),
                It.IsAny<string[]>()),
            Times.Once
        );
    }


    [Fact]
    public async Task GetProducts_WithSearchTerm_FiltersByNameSkuOrInternalCode()
    {
        // ARRANGE: preparar productos con diferentes nombres y códigos
        var products = new List<Product>
    {
        new Product(
            "SKU-001",
            "Mouse Gamer",
            15000m,
            10,
            internalCode: "INT-001"
        ),
        new Product(
            "SKU-002",
            "Teclado Mecánico",
            25000m,
            5,
            internalCode: "INT-002"
        ),
        new Product(
            "SKU-003",
            "Monitor LED",
            100000m,
            3,
            internalCode: "INT-003"
        )
    };

        var repositoryMock = new Mock<IRepository>();

        repositoryMock
            .Setup(r => r.GetFiltered<Product>(
                It.IsAny<Expression<Func<Product, bool>>>(),
                It.IsAny<string[]>()))
            .ReturnsAsync((
                Expression<Func<Product, bool>> predicate,
                string[] include) =>
                products.Where(predicate.Compile()).ToList());

        var service = new ProductsManagementService(
            repositoryMock.Object,
            NullLogger<ProductsManagementService>.Instance
        );

        var filter = new ProductModel.FilterProduct(
            Status: null,
            Search: "mouse",
            PageNumber: 1,
            PageSize: 10
        );

        // ACT: realizar la búsqueda
        var result = await service.GetProducts(filter);

        // ASSERT: debe encontrar Mouse Gamer sin distinguir mayúsculas
        Assert.Single(result.ProductItems);
        Assert.Equal("Mouse Gamer", result.ProductItems[0].Name);
        Assert.Equal(1, result.Total);
    }

}
