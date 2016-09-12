using System;
using System.Globalization;
using System.Web.Mvc;

namespace Auth0.IdinConnectorSample
{
    public class DecimalModelBinder : DefaultModelBinder
    {
        public override object BindModel(ControllerContext controllerContext, ModelBindingContext bindingContext)
        {
            var valueProviderResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);

            string val = valueProviderResult.AttemptedValue;

            val = val.Replace(",", CultureInfo.CurrentCulture.NumberFormat.CurrencyDecimalSeparator);
            val = val.Replace(".", CultureInfo.CurrentCulture.NumberFormat.CurrencyDecimalSeparator);

            decimal d;
            Decimal.TryParse(val, out d);

            return d == 0 ? base.BindModel(controllerContext, bindingContext) : d;
        }
    }
}