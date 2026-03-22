using FluentValidation;
using Store.Application.DTOs.Orders.Requests;
using System;
using System.Collections.Generic;
using System.Text;

namespace Store.Application.Validators.Orders;

public class CreateOrderRequestValidator:AbstractValidator<OrderCreateRequest>
{

    public CreateOrderRequestValidator()
    {
        RuleFor(r => r.CustomerId).NotEmpty().WithMessage("The customerId is required.");
        RuleFor(r => r.Items).NotEmpty().WithMessage("The order must has at least one item.");
        RuleForEach(r => r.Items).ChildRules(i =>
        {
            i.RuleFor(i => i.ProductId).NotEmpty().WithMessage("ProductId is required.");
            i.RuleFor(i => i.Quantity).GreaterThan(0).WithMessage("The Quantity must at least 1. unit");
            i.RuleFor(i => i.UnitPrice).GreaterThan(0m).WithMessage("The price of the product unit can be 0");

        });
    }
}
