import { langCountryCode } from '../utils/language';

interface LangFlagProps {
    code: string;
    className?: string;
}

const LangFlag: React.FC<LangFlagProps> = ({ code, className = '' }) => {
    const country = langCountryCode(code);
    if (!country) return null;
    return <span className={`fi fi-${country} ${className}`} />;
};

export default LangFlag;
