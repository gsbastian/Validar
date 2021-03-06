using System;
using System.Collections;
using System.ComponentModel;
using System.Linq;
using GenericTemplates.Sandra;
using Sandra.SimpleValidator;
using Scalpel;

namespace TemplatesGeneric.Sandra
{

    [Remove]
    public class ValidationTemplate<T> : 
        IDataErrorInfo, 
        INotifyDataErrorInfo where T : INotifyPropertyChanged
    {
        INotifyPropertyChanged target;
        IModelValidator validator;
        ValidationResult validationResult;

        public ValidationTemplate(INotifyPropertyChanged target)
        {
            this.target = target;
            validator = ValidationFactory.GetValidator<T>();
            validationResult = validator.Validate(target);
            target.PropertyChanged += Validate;
        }

        void Validate(object sender, PropertyChangedEventArgs e)
        {
            validationResult = validator.Validate(target);
            foreach (var error in validationResult.Messages)
            {
                RaiseErrorsChanged(error.PropertyName);
            }
        }

        public IEnumerable GetErrors(string propertyName)
        {
            return validationResult.Messages
                                   .Where(x => x.PropertyName == propertyName)
                                   .Select(x => x.Message);
        }

        public bool HasErrors
        {
            get { return validationResult.IsInvalid; }
        }

        public string Error
        {
            get
            {
                var strings = validationResult.Messages.Select(x => x.Message)
                                              .ToArray();
                return string.Join(Environment.NewLine, strings);
            }
        }

        public string this[string propertyName]
        {
            get
            {
                var strings = validationResult.Messages.Where(x => x.PropertyName == propertyName)
                                              .Select(x => x.Message)
                                              .ToArray();
                return string.Join(Environment.NewLine, strings);
            }
        }

        public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

        void RaiseErrorsChanged(string propertyName)
        {
            var handler = ErrorsChanged;
            if (handler != null)
            {
                handler(this, new DataErrorsChangedEventArgs(propertyName));
            }
        }
    }
}