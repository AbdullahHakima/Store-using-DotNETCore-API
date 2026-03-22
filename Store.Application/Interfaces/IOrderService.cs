using Store.Application.Common;
using Store.Application.DTOs.Orders.Requests;
using Store.Application.DTOs.Orders.Responses;
using System;
using System.Collections.Generic;
using System.Text;

namespace Store.Application.Interfaces;

public interface IOrderService
{
    public Task<Result<OrderDetailResponse>> GetByIdAsync(Guid id);
    public Task<Result<OrderCreateResponse>> CreateAsync(OrderCreateRequest request);
    public Task<Result<OrderConfirmResponse>> ConfirmWithStockAsync(Guid id);
}
