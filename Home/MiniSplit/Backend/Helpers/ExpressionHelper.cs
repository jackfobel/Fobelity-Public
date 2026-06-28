using Newtonsoft.Json;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using MetaLinq;

namespace BackendServices.Helpers
{
  public static class ExpressionHelper
  {
    public static string AccessDebugView(Expression expression)
    {
      var debugViewProperty = typeof(Expression).GetProperty("DebugView", BindingFlags.Instance | BindingFlags.NonPublic);
      var debugView = debugViewProperty.GetValue(expression);
      return debugView.ToString();
    }

    public static string SerializeExpression(Expression expression)
    {
      var editableExpression = EditableExpression.CreateEditableExpression(expression);
      return JsonConvert.SerializeObject(editableExpression);
    }

    public static Expression DeserializeExpression(string json)
    {
      var editableExpression = JsonConvert.DeserializeObject<EditableExpression>(json);
      return editableExpression.ToExpression();
    }
  }
}
