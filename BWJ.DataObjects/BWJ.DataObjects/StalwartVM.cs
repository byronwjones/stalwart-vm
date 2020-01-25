using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BWJ.DataObjects
{
    /// <summary>
    /// View model base class.  Ideally, view models derived from this class are only instanciated once
    /// and remain in place for the lifetime of the application.
    /// </summary>
    public abstract class StalwartVM : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Declares a supporting property - that is, one that another property is dependent upon. 
        /// </summary>
        /// <remarks>Intended to be invoked within a value computation function inside a get accessor that computes 
        /// its returned value using properties external to this object.
        /// Useful when dependance upon a collection, collection item, or some other view model's property is desired,
        /// as this object cannot automatically detect when such external properties are being used.
        /// This method should also be used to declare supporting properties invoked within code blocks 
        /// which may not necessarily be called every time the value computation function is invoked 
        /// (e.g. conditional statements)</remarks>
        /// <param name="property">Supporting property name</param>
        protected void DependsUpon(string property)
        {
            EvaluatePotentialSupportingProperty(property);
        }

        /// <summary>
        /// Causes the PropertyChanged event to be raised for every dependent property, that is, one with a
        /// value computed in part by using the property with the given name
        /// </summary>
        /// <param name="supportingProperty">Property name</param>
        protected void NotifyDependentProperties(string supportingProperty)
        {
            //notify change for any properties that may change when this one does
            if (changeSubscriptions.Keys.Contains(supportingProperty))
            {
                foreach (var notify in changeSubscriptions[supportingProperty])
                    NotifyChanged(notify);
            }
        }

        /// <summary>
        /// Raises PropertyChanged event for the property with the given name, and subsequently raises the
        /// PropertyChanged event any property dependent on the given property
        /// </summary>
        /// <param name="propertyName">Property name</param>
        protected void NotifyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            NotifyDependentProperties(propertyName);
        }

        /// <summary>
        /// Declares the given property as potentially supporting another property,
        /// while also returning the property's value
        /// </summary>
        /// <typeparam name="T">Property type</typeparam>
        /// <param name="property">Property name</param>
        /// <param name="returnValue">Property value</param>
        /// <returns>Property value</returns>
        protected T get<T>(string property, T returnValue)
        {
            EvaluatePotentialSupportingProperty(property);
            return returnValue;
        }

        /// <summary>
        /// Computes and returns the value of the property with the given name.
        /// </summary>
        /// <typeparam name="T">Property type</typeparam>
        /// <param name="property">Property name</param>
        /// <param name="valueComputationFunc">Function computing the value returned by the given
        /// property's get accessor.</param>
        /// <remarks>The properties for any get accessors invoked during the invocation of this
        /// function will be considered supporters of the given property</remarks>
        protected T get<T>(string property, Func<T> valueComputationFunc)
        {
            // a dependent property can also be a supporting property if it is called during the invocation
            // of some other property's getter
            EvaluatePotentialSupportingProperty(property);

            //only configure a dependent property once
            if (configuredGetters.Any(g => g == property))
            {
                return valueComputationFunc();
            }

            configuredGetters.Add(property);
            // add this property as a dependent property of any supporting property found during configuration
            currentGetter.Push(property);
            // compute property value while identifying all supporting properties
            T val = valueComputationFunc();
            // done configuring this property
            currentGetter.Pop();

            return val;
        }

        /// <summary>
        /// Determines if the given property is a supporting property, configuring it as such if it is
        /// </summary>
        /// <remarks>This method is called whenever any get accessor implementing
        /// either <see cref="get{T}(string, T)"/> or <seealso cref="get{T}(string, Func{T})"/> is invoked.
        /// If the get accessor for the property calling this method (A) was invoked during the invocation of 
        /// the get accessor for another property (B), it is understood that (B) depends on (A) to compute its value, 
        /// meaning that (A) is in fact a supporting property.</remarks>
        /// <param name="property">Name of property being evaluated</param>
        private void EvaluatePotentialSupportingProperty(string property)
        {
            //subscribing to changes of a property already in the
            //getter stack will cause a circular reference, so its not allowed
            if (currentGetter.Count > 0 && !currentGetter.Contains(property))
                SubscribeToChange(property, currentGetter.Peek());
        }

        /// <summary>
        /// Configure the given dependent property to raise the PropertyChanged event whenever the given
        /// supporting property's value changes
        /// </summary>
        /// <remarks>
        /// The basic idea here is that properties with a value computed from other properties
        /// depend on those properties -- they are DEPENDENTS. The properties that dependent properties
        /// use to compute their values are SUPPORTERS. A dependent property's value has likely changed if any 
        /// of its supporting properties' values have changed.  So when supporting property changes,
        /// events are raised notifying subscribers of not only that change, but also to changes of 
        /// its dependent properties.
        /// A list is maintained for each supporting property containing all of its dependent properties. 
        /// When we raise PropertyChanged for a supporting property, we use the list to raise
        /// PropertyChanged for each of its dependent properties.
        /// </remarks>
        private void SubscribeToChange(string supportingProperty, string dependentProperty)
        {
            //prevent circular references, since a dependent property could also be a
            //supporting property
            if (changeSubscriptions.Keys.Contains(dependentProperty))
            {
                if (changeSubscriptions[dependentProperty].Contains(supportingProperty))
                    return;
            }
            //if property given is already registered as a supporting property, add to its list of dependents,
            // else register the supporting property and add the dependent to it
            if (changeSubscriptions.Keys.Contains(supportingProperty))
                changeSubscriptions[supportingProperty].Add(dependentProperty);
            else
                changeSubscriptions.Add(supportingProperty,
                    new HashSet<string> { dependentProperty });
        }

        /// <summary>
        /// Copies the items from the given source to the given target
        /// </summary>
        protected void CopyToList<T>(IList<T> target, IEnumerable<T> source)
        {
            if (target == null || source == null) { return; }
            target.Clear();

            foreach (var item in source)
            {
                target.Add(item);
            }
        }

        /// <summary>
        /// A dictionary of supporting properties, and a hash set of their dependents
        /// </summary>
        private Dictionary<string, HashSet<string>> changeSubscriptions =
            new Dictionary<string, HashSet<string>>();

        private HashSet<string> configuredGetters = new HashSet<string>();
        private Stack<string> currentGetter = new Stack<string>();
    }

    /// <summary>
    /// Base class for a view model intended to derive its values from and potentially write its values back
    /// to a simple entity object
    /// </summary>
    public abstract class StalwartVM<Entity> : StalwartVM
        where Entity : new()
    {
        /// <summary>
        /// Updates an entity object with the values from this view model
        /// </summary>
        /// <remarks>If an object was provided to this view model's constructor, or to the last call to
        /// <see cref="BindEntity(Entity)"/>, the same object instance will be updated and returned.
        /// If this method is overriden, it should call the base method in order to behave properly.
        /// </remarks>
        /// <returns>Entity object</returns>
        public virtual Entity ToEntity()
        {
            CustomizeToEntity(Data);
            return Data;
        }

        /// <summary>
        /// Populates this view model with values from the given object
        /// </summary>
        public virtual void BindEntity(Entity entity)
        {
            Data = entity;
            CustomizeBindEntity(entity);
        }

        /// <summary>
        /// Provided as an entry point for developers to introduce additional logic in the
        /// <see cref="ToEntity"/> method in scenarios where objects derived from this class are generated by
        /// a code templating tool
        /// </summary>
        protected virtual void CustomizeToEntity(Entity entity) { }

        /// <summary>
        /// Provided as an entry point for developers to introduce additional logic in the
        /// <see cref="BindEntity(Entity)"/> method in scenarios where objects derived from this class
        /// are generated by a code templating tool
        /// </summary>
        protected virtual void CustomizeBindEntity(Entity entity) { }

        private Entity Data;
    }
}

