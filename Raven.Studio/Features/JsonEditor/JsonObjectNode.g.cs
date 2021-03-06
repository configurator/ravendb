namespace Raven.Studio.Features.JsonEditor {
    using ActiproSoftware.Text.Parsing;
    using ActiproSoftware.Text.Parsing.Implementation;
    using System;
    using System.Collections.Generic;
    
    
    /// <summary>
    /// Represents a json object.
    /// </summary>
    /// <remarks>
    /// This type was generated by the Actipro Language Designer tool v12.1.561.0 (http://www.actiprosoftware.com).
    /// </remarks>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("LanguageDesigner", "12.1.561.0")]
    public partial class JsonObjectNode : AstNodeBase {
        
        /// <summary>
        /// Gets the property values.
        /// </summary>
        private IList<JsonPropertyValuePairNode> propertyValuesValue;
        
        /// <summary>
        /// Gets the An integer value that identifies the type of AST node.
        /// </summary>
        /// <value>The An integer value that identifies the type of AST node.</value>
        public override Int32 Id {
            get {
                return JsonAstNodeId.JsonObjectNode;
            }
        }
        
        /// <summary>
        /// Gets the property values.
        /// </summary>
        /// <value>The property values.</value>
        public IList<JsonPropertyValuePairNode> PropertyValues {
            get {
                if ((this.propertyValuesValue == null)) {
                    this.propertyValuesValue = new List<JsonPropertyValuePairNode>();
                }
                return this.propertyValuesValue;
            }
        }
        
        /// <summary>
        /// Returns whether the <see cref="PropertyValues"/> collection property contains at least one item.
        /// </summary>
        /// <value><c>true</c> if there is at least one item in the collection; otherwise, <c>false</c>.</value>
        public Boolean HasPropertyValues {
            get {
                if (((this.propertyValuesValue != null) 
                            && (this.propertyValuesValue.Count > 0))) {
                    return true;
                }
                else {
                    return false;
                }
            }
        }
        
        /// <summary>
        /// Retrieves an <c>IEnumerator</c> object that can iterate the child <see cref="IAstNode"/> objects in this node.
        /// </summary>
        /// <returns>An <c>IEnumerator</c> object that can iterate the child <see cref="IAstNode"/> objects in this node.</returns>
        protected override IEnumerator<IAstNode> GetChildrenEnumerator() {
            IEnumerator<IAstNode> baseEnumerator = base.GetChildrenEnumerator();
            if ((baseEnumerator != null)) {
				while (baseEnumerator.MoveNext())
					yield return baseEnumerator.Current;
            }
            if ((this.propertyValuesValue != null)) {
				foreach (IAstNode propertyValuesValueItem in this.propertyValuesValue)
					if (propertyValuesValueItem != null) yield return propertyValuesValueItem;
            }
        }
    }
}
