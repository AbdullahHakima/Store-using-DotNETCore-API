using FluentValidation;
using Store.Application.DTOs.Products.Requests;
using System;
using System.Collections.Generic;
using System.Text;

namespace Store.Application.Validators.Products
{
    public class ProductSearchRequestValidator:AbstractValidator<ProductSearchRequest>
    {
        public ProductSearchRequestValidator()
        {
            RuleFor(r => r.Page).GreaterThanOrEqualTo(1).WithMessage("There is no pages less than 1.");
            RuleFor(r => r.PageSize).LessThanOrEqualTo(50).WithMessage("The maximum allowd is 50 per page");
            RuleFor(r => r.MinPrice).GreaterThan(0m).WithMessage("The minPrice must be greater than 0m").When(r=>r.MinPrice.HasValue);
            RuleFor(r => r.MaxPrice).GreaterThan(0m).LessThanOrEqualTo(1_000_000m).When(r => r.MaxPrice.HasValue);
            RuleFor(r => r.MaxPrice).GreaterThanOrEqualTo(r => r.MinPrice!.Value)
                                    .WithMessage("MaxPrice must be greater than or equal MinPrice")
                                    .When(r => r.MaxPrice.HasValue && r.MinPrice.HasValue);
        }
    }
}
