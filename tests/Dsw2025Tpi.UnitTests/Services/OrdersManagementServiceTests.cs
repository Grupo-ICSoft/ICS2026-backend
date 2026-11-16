
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dsw2025Tpi.Application.Dtos;
using Dsw2025Tpi.Application.Services;
using Dsw2025Tpi.Domain.Entities;
using Dsw2025Tpi.Domain.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Dsw2025Tpi.UnitTests.Services;

public class OrdersManagementServiceTests
{
    [Fact]
    public async Task CreateOrder_WithValidItemsAndStock_CalculatesTotalAndDecrementsProductStock()
    {
        // ARRANGE: preparar el cliente y los productos
        var customerId = Guid.NewGuid();

        var customer = new Customer
        {
            Id = customerId,
            Name = "Cliente de prueba",
            Email = "cliente@example.com"
        };

        var product1 = new Product(
            "SKU-001", "Mouse Gamer", 15000m, 10);

        var product2 = new Product(
            "SKU-002", "Teclado", 25000m, 8);

        var request = new OrderRequest(
            CustomerId: customerId,
            ShippingAddress: "Dirección de envío",
            BillingAddress: "Dirección de facturación",
            Notes: "Orden de prueba",
            Items: new List<OrderItemRequest>
            {
                new(product1.Id, 2),
                new(product2.Id, 1)
            }
        );

        var repositoryMock = new Mock<IRepository>();

        repositoryMock
            .Setup(r => r.GetById<Customer>(
                customerId,
                It.IsAny<string[]>()))
            .ReturnsAsync(customer);

        repositoryMock
            .Setup(r => r.GetById<Product>(
                product1.Id,
                It.IsAny<string[]>()))
            .ReturnsAsync(product1);

        repositoryMock
            .Setup(r => r.GetById<Product>(
                product2.Id,
                It.IsAny<string[]>()))
            .ReturnsAsync(product2);

        var service = new OrdersManagementService(
            repositoryMock.Object,
            NullLogger<OrdersManagementService>.Instance
        );

        // ACT: crear la orden
        var result = await service.CreateOrder(request);

        // ASSERT: verificar el total y el stock
        Assert.NotNull(result);
        Assert.Equal(55000m, result.TotalAmount);
        Assert.Equal(8, product1.StockQuantity);
        Assert.Equal(7, product2.StockQuantity);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(30000m, result.Items[0].Subtotal);
        Assert.Equal(25000m, result.Items[1].Subtotal);

        // Verificar una actualización por producto
        repositoryMock.Verify(
            r => r.Update(It.IsAny<Product>()),
            Times.Exactly(2)
        );

        repositoryMock.Verify(
            r => r.Update(It.Is<Product>(p =>
                p.Id == product1.Id)),
            Times.Once
        );

        repositoryMock.Verify(
            r => r.Update(It.Is<Product>(p =>
                p.Id == product2.Id)),
            Times.Once
        );

        // Verificar que la orden se guarda una sola vez
        repositoryMock.Verify(
            r => r.Add(It.Is<Order>(o =>
                o.TotalAmount == 55000m &&
                o.OrderItems != null &&
                o.OrderItems.Count == 2
            )),
            Times.Once
        );
    }

    [Fact]
    public async Task CreateOrder_WhenProductStockIsInsufficient_ThrowsInsufficientStockException()
    {
        // ARRANGE: cliente existente y producto con stock insuficiente
        var customerId = Guid.NewGuid();
        var customer = new Customer
        {
            Id = customerId,
            Name = "Cliente de prueba",
            Email = "cliente@example.com"
        };

        var product = new Product(
            "SKU-003",
            "Monitor Gamer",
            100000m,
            2
        );

        var request = new OrderRequest(
            CustomerId: customerId,
            ShippingAddress: "Dirección de envío",
            BillingAddress: "Dirección de facturación",
            Notes: null,
            Items: new List<OrderItemRequest>
            {
            new(product.Id, 5)
            }
        );

        var repositoryMock = new Mock<IRepository>();

        repositoryMock
            .Setup(r => r.GetById<Customer>(
                customerId,
                It.IsAny<string[]>()))
            .ReturnsAsync(customer);

        repositoryMock
            .Setup(r => r.GetById<Product>(
                product.Id,
                It.IsAny<string[]>()))
            .ReturnsAsync(product);

        var service = new OrdersManagementService(
            repositoryMock.Object,
            NullLogger<OrdersManagementService>.Instance
        );

        // ACT: intentar crear la orden sin stock suficiente
        var exception = await Assert.ThrowsAsync<
            Dsw2025Tpi.Application.Exceptions.InsufficientStockException>(
            () => service.CreateOrder(request)
        );

        // ASSERT: verificar que se rechazó sin modificar el producto
        Assert.NotNull(exception);
        Assert.Equal(2, product.StockQuantity);

        repositoryMock.Verify(
            r => r.Update(It.IsAny<Product>()),
            Times.Never
        );

        repositoryMock.Verify(
            r => r.Add(It.IsAny<Order>()),
            Times.Never
        );
    }

    [Fact]
    public async Task CreateOrder_WhenCustomerDoesNotExist_ThrowsCustomerNotFoundException()
    {
        // ARRANGE: preparar una solicitud cuyo cliente no existe
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var request = new OrderRequest(
            CustomerId: customerId,
            ShippingAddress: "Dirección de envío",
            BillingAddress: "Dirección de facturación",
            Notes: null,
            Items: new List<OrderItemRequest>
            {
            new(productId, 1)
            }
        );

        var repositoryMock = new Mock<IRepository>();

        // El repositorio indica que el cliente no existe
        repositoryMock
            .Setup(r => r.GetById<Customer>(
                customerId,
                It.IsAny<string[]>()))
            .ReturnsAsync((Customer?)null);

        var service = new OrdersManagementService(
            repositoryMock.Object,
            NullLogger<OrdersManagementService>.Instance
        );

        // ACT: intentar crear la orden
        var exception = await Assert.ThrowsAsync<
            Dsw2025Tpi.Application.Exceptions.CustomerNotFoundException>(
            () => service.CreateOrder(request)
        );

        // ASSERT: comprobar que se rechazó la solicitud
        Assert.NotNull(exception);

        // No debe consultar productos
        repositoryMock.Verify(
            r => r.GetById<Product>(
                It.IsAny<Guid>(),
                It.IsAny<string[]>()),
            Times.Never
        );

        // No debe modificar ningún producto
        repositoryMock.Verify(
            r => r.Update(It.IsAny<Product>()),
            Times.Never
        );

        // No debe guardar ninguna orden
        repositoryMock.Verify(
            r => r.Add(It.IsAny<Order>()),
            Times.Never
        );
    }
}
